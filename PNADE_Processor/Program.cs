using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FixedWidthParser
{
	public class VariableDefinition
	{
		public int StartPosition { get; set; }
		public int Length { get; set; }
		public string VariableCode { get; set; }
		public string Description { get; set; }
		public Dictionary<string, string> ValueLabels { get; set; } = new Dictionary<string, string>();

		public string GetFormattedValue(string rawValue)
		{
			if (ValueLabels.TryGetValue(rawValue, out var label))
			{
				return $"{rawValue} - {label}";
			}
			return rawValue;
		}
	}

	public class FixedWidthRecord
	{
		public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();

		public override string ToString()
		{
			return string.Join(Environment.NewLine, Fields.Select(kv => $"{kv.Key}: {kv.Value}"));
		}
	}

	public class FixedWidthParser
	{
		public List<VariableDefinition> VariableDefinitions { get; private set; } = new List<VariableDefinition>();

		public void LoadVariableDefinitions(string csvPath)
		{
			var lines = File.ReadAllLines(csvPath);
			VariableDefinitions.Clear();

			VariableDefinition currentVar = null;

			// Pular cabeçalho (linha 0) e começar da linha 1
			foreach (var line in lines.Skip(1))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;

				var parts = ParseCsvLine(line);

				// Se tem posição inicial e tamanho, é uma nova variável
				if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
				{
					if (currentVar != null)
					{
						VariableDefinitions.Add(currentVar);
					}

					currentVar = new VariableDefinition
					{
						StartPosition = int.Parse(parts[0]),
						Length = int.Parse(parts[1]),
						VariableCode = parts[2].Trim(),
						Description = parts.Length > 4 ? parts[4].Trim() : string.Empty
					};
				}

				// Se é uma linha com valores/categorias (colunas 5 e 6 preenchidas)
				if (currentVar != null && parts.Length >= 6 &&
					!string.IsNullOrWhiteSpace(parts[5]) && !string.IsNullOrWhiteSpace(parts[6]))
				{
					currentVar.ValueLabels[parts[5].Trim()] = parts[6].Trim();
				}
			}

			// Adicionar a última variável
			if (currentVar != null)
			{
				VariableDefinitions.Add(currentVar);
			}

			// Ordenar por posição inicial para garantir a ordem correta de leitura
			VariableDefinitions = VariableDefinitions.OrderBy(v => v.StartPosition).ToList();
		}

		private string[] ParseCsvLine(string line)
		{
			var parts = new List<string>();
			bool inQuotes = false;
			int start = 0;

			for (int i = 0; i < line.Length; i++)
			{
				if (line[i] == '"')
				{
					inQuotes = !inQuotes;
				}
				else if (line[i] == ',' && !inQuotes)
				{
					parts.Add(line.Substring(start, i - start).Trim());
					start = i + 1;
				}
			}

			// Adicionar a última parte
			parts.Add(line.Substring(start).Trim());

			return parts.ToArray();
		}

		public List<FixedWidthRecord> ParseFixedWidthFile(string filePath)
		{
			var records = new List<FixedWidthRecord>();
			var lines = File.ReadAllLines(filePath);

			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line)) continue;

				var record = new FixedWidthRecord();

				foreach (var varDef in VariableDefinitions)
				{
					// Ajustar posição para base 0
					int start = varDef.StartPosition - 1;
					if (start >= line.Length) continue;

					int length = Math.Min(varDef.Length, line.Length - start);
					string rawValue = line.Substring(start, length).Trim();

					// Armazenar tanto o valor bruto quanto o formatado (se houver labels)
					string formattedValue = varDef.GetFormattedValue(rawValue);
					record.Fields[varDef.VariableCode] = formattedValue;
				}

				records.Add(record);
			}

			return records;
		}

		public void SaveAsCsv(List<FixedWidthRecord> records, string outputPath, bool includeLabels = true)
		{
			if (records == null || records.Count == 0) return;

			// Obter todos os códigos de variáveis únicos
			var allVariables = VariableDefinitions.Select(v => v.VariableCode).Distinct().ToList();

			using (var writer = new StreamWriter(outputPath))
			{
				// Escrever cabeçalho
				writer.WriteLine(string.Join(",", allVariables));

				// Escrever dados
				foreach (var record in records)
				{
					var line = new List<string>();
					foreach (var variable in allVariables)
					{
						if (record.Fields.TryGetValue(variable, out var value))
						{
							// Se não queremos incluir os labels, pegar apenas o valor antes do " - "
							if (!includeLabels && value.Contains(" - "))
							{
								value = value.Split(new[] { " - " }, StringSplitOptions.None)[0];
							}

							// Escapar vírgulas no valor
							line.Add(value.Contains(",") ? $"\"{value}\"" : value);
						}
						else
						{
							line.Add("");
						}
					}
					writer.WriteLine(string.Join(",", line));
				}
			}
		}
	}

	class Program
	{
		static void Main(string[] args)
		{

			string basePath = @"E:\Estudos\PUC\7periodo\PNAD";
			string mappingCsvPath = Path.Combine(basePath, "pns_mapping.csv");
			string dataFilePath = Path.Combine(basePath, "pns_data - Cut.txt");
			string outputCsvPath = Path.Combine(basePath, "pns_parsed.csv");


			var parser = new FixedWidthParser();

			try
			{
				Console.WriteLine("Iniciando processamento dos dados da PNAD...");

				// 1. Carregar definições das variáveis
				Console.WriteLine($"\n[1/3] Carregando definições de variáveis de: {mappingCsvPath}");
				parser.LoadVariableDefinitions(mappingCsvPath);
				Console.WriteLine($"Carregadas {parser.VariableDefinitions.Count} definições de variáveis.");

				// Exibir algumas variáveis para verificação
				Console.WriteLine("\nAlgumas variáveis carregadas:");
				foreach (var varDef in parser.VariableDefinitions.Take(5))
				{
					Console.WriteLine($"{varDef.VariableCode} (Pos: {varDef.StartPosition}, Tam: {varDef.Length}): {varDef.Description}");
					if (varDef.ValueLabels.Count > 0)
					{
						Console.WriteLine($"  Valores possíveis: {string.Join(", ", varDef.ValueLabels.Take(3).Select(kv => $"{kv.Key}={kv.Value}"))}...");
					}
				}

				// 2. Processar arquivo de dados
				Console.WriteLine($"\n[2/3] Processando arquivo de dados: {dataFilePath}");
				var records = parser.ParseFixedWidthFile(dataFilePath);
				Console.WriteLine($"Processados {records.Count} registros.");

				// 3. Salvar como CSV
				Console.WriteLine($"\n[3/3] Salvando dados convertidos em: {outputCsvPath}");
				parser.SaveAsCsv(records, outputCsvPath, includeLabels: true);
				Console.WriteLine("Conversão concluída com sucesso!");

				// Exibir dados do primeiro registro para demonstração
				Console.WriteLine("\nAmostra dos dados do primeiro registro:");
				Console.WriteLine(records[0]);

				Console.WriteLine("\nPressione qualquer tecla para sair...");
				Console.ReadKey();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"\nErro durante o processamento:");
				Console.WriteLine(ex.Message);
				if (ex.InnerException != null)
				{
					Console.WriteLine($"Detalhes: {ex.InnerException.Message}");
				}
				Console.WriteLine("\nPressione qualquer tecla para sair...");
				Console.ReadKey();
			}
		}
	}
}
