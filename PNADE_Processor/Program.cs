using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


public class VariableDefinition
{
	public int StartPosition { get; set; }
	public int Length { get; set; }
	public string VariableCode { get; set; }
	public string Description { get; set; }
	public Dictionary<string, string> ValueLabels { get; set; } = new Dictionary<string, string>();
}

public class FieldData
{
	public string RawValue { get; set; }
	public string FormattedValue { get; set; } // Valor formatado com descrição (opcional)
	public string ValueDescription { get; set; } // Descrição separada

	public FieldData(string rawValue, string description = null)
	{
		RawValue = rawValue;
		ValueDescription = description;
		FormattedValue = description != null ? $"{rawValue} - {description}" : rawValue;
	}
}

public class FixedWidthRecord
{
	public Dictionary<string, FieldData> Fields { get; set; } = new Dictionary<string, FieldData>();

	public override string ToString()
	{
		return string.Join(Environment.NewLine, Fields.Select(kv => $"{kv.Key}: {kv.Value.RawValue} ({kv.Value.ValueDescription})"));
	}
}

public class FixedWidthParser
{
	public List<VariableDefinition> VariableDefinitions { get; private set; } = new List<VariableDefinition>();
	public Dictionary<string, string> VariableDescriptions { get; private set; } = new Dictionary<string, string>();

	public void LoadVariableDefinitions(string csvPath)
	{
		var lines = File.ReadAllLines(csvPath);
		VariableDefinitions.Clear();
		VariableDescriptions.Clear();

		VariableDefinition currentVar = null;

		foreach (var line in lines.Skip(1)) // Pular cabeçalho
		{
			if (string.IsNullOrWhiteSpace(line)) continue;

			var parts = ParseCsvLine(line);

			// Nova variável quando tem posição e tamanho
			if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
			{
				if (currentVar != null)
				{
					VariableDefinitions.Add(currentVar);
					VariableDescriptions[currentVar.VariableCode] = currentVar.Description;
				}

				currentVar = new VariableDefinition
				{
					StartPosition = int.Parse(parts[0]),
					Length = int.Parse(parts[1]),
					VariableCode = parts[2].Trim(),
					Description = parts.Length > 4 ? parts[4].Trim() : string.Empty
				};
			}

			// Valores possíveis (quando colunas 5 e 6 preenchidas)
			if (currentVar != null && parts.Length >= 6 &&
				!string.IsNullOrWhiteSpace(parts[5]) && !string.IsNullOrWhiteSpace(parts[6]))
			{
				currentVar.ValueLabels[parts[5].Trim()] = parts[6].Trim();
			}
		}

		if (currentVar != null)
		{
			VariableDefinitions.Add(currentVar);
			VariableDescriptions[currentVar.VariableCode] = currentVar.Description;
		}

		VariableDefinitions = VariableDefinitions.OrderBy(v => v.StartPosition).ToList();
	}

	private string[] ParseCsvLine(string line)
	{
		var parts = new List<string>();
		bool inQuotes = false;
		int start = 0;

		for (int i = 0; i < line.Length; i++)
		{
			if (line[i] == '"') inQuotes = !inQuotes;
			else if (line[i] == ',' && !inQuotes)
			{
				parts.Add(line.Substring(start, i - start).Trim());
				start = i + 1;
			}
		}

		parts.Add(line.Substring(start).Trim());
		return parts.ToArray();
	}

	public List<FixedWidthRecord> ParseFixedWidthFile(string filePath)
	{
		var records = new List<FixedWidthRecord>();
		var lines = File.ReadAllLines(filePath);

		foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
		{
			var record = new FixedWidthRecord();

			foreach (var varDef in VariableDefinitions)
			{
				int start = varDef.StartPosition - 1;
				if (start >= line.Length) continue;

				int length = Math.Min(varDef.Length, line.Length - start);
				string rawValue = line.Substring(start, length).Trim();
				string description = varDef.ValueLabels.TryGetValue(rawValue, out var desc) ? desc : null;

				record.Fields[varDef.VariableCode] = new FieldData(rawValue, description);
			}

			records.Add(record);
		}

		return records;
	}

	public void SaveAsCsv(List<FixedWidthRecord> records, string outputPath,
					 bool includeDescriptions = false,
					 bool useDescriptionsInHeaders = false,
					 string separator = "|")
	{
		if (records == null || !records.Any()) return;

		// Obter as variáveis presentes nos registros
		var variables = records.First().Fields.Keys.ToList();

		using (var writer = new StreamWriter(outputPath))
		{
			// Cabeçalho
			if (useDescriptionsInHeaders)
			{
				var headers = variables.Select(v =>
				{
					string description = VariableDescriptions.ContainsKey(v) ? VariableDescriptions[v] : string.Empty;
					return EscapeCsvField($"{v} - {description}", separator);
				});
				writer.WriteLine(string.Join(separator, headers));
			}
			else
			{
				var headers = variables.Select(v => EscapeCsvField(v, separator));
				writer.WriteLine(string.Join(separator, headers));
			}

			// Dados
			foreach (var record in records)
			{
				var line = variables.Select(v =>
				{
					if (record.Fields.TryGetValue(v, out var field))
					{
						var value = includeDescriptions ? field.FormattedValue : field.RawValue;
						return EscapeCsvField(value, separator);
					}
					return "";
				});

				writer.WriteLine(string.Join(separator, line));
			}
		}
	}

	private string EscapeCsvField(string value, string separator)
	{
		if (string.IsNullOrEmpty(value))
			return "";

		// Se o valor contiver o separador, quebras de linha ou aspas, colocar entre aspas
		if (value.Contains(separator) || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
		{
			return $"\"{value.Replace("\"", "\"\"")}\"";
		}
		return value;
	}


	class Program
	{
		static void Main(string[] args)
		{
			string basePath = @"E:\Estudos\PUC\7periodo\PNAD";
			string mappingCsvPath = Path.Combine(basePath, "pns_mapping.csv");
			string dataFilePath = Path.Combine(basePath, "pns_data - Cut.txt");
			string outputCsvPath = Path.Combine(basePath, "pns_parsed.csv");
			string filteredOutputCsvPath = Path.Combine(basePath, "pns_filtered.csv");

			var parser = new FixedWidthParser();

			try
			{
				Console.WriteLine("Iniciando processamento dos dados da PNAD...");

				// 1. Carregar definições das variáveis
				Console.WriteLine($"\n[1/3] Carregando definições de variáveis de: {mappingCsvPath}");
				parser.LoadVariableDefinitions(mappingCsvPath);
				Console.WriteLine($"Carregadas {parser.VariableDefinitions.Count} definições de variáveis.");

				// 2. Processar arquivo de dados
				Console.WriteLine($"\n[2/3] Processando arquivo de dados: {dataFilePath}");
				var records = parser.ParseFixedWidthFile(dataFilePath);
				Console.WriteLine($"Processados {records.Count} registros.");

				// Lista de variáveis desejadas para filtro
				List<string> desiredVariables = new List<string>
			{
				"V0001", "V0026", "A001", "A005010", "A005012", "A009010",
				"P027", "P050", "Q050", "Q06306", "Q06307", "Q06308",
				"Q06309", "Q06310", "Q08607", "VDD004A", "VDE014", "VDF004",
				"P00104", "P00404", "Q060", "C006"
			};

				// 3. Filtrar os registros
				Console.WriteLine("\n[3/3] Filtrando registros pelas variáveis selecionadas...");
				var processor = new DataProcessor(records);
				var filteredRecords = processor.FilterRecords(desiredVariables);
				Console.WriteLine($"Filtro aplicado. Mantidas {desiredVariables.Count} variáveis de {parser.VariableDefinitions.Count} disponíveis.");

				// 4. Salvar como CSV
				//Console.WriteLine($"\n[4/3] Salvando dados convertidos em: {outputCsvPath}");
				//parser.SaveAsCsv(records, outputCsvPath);

				//Console.WriteLine($"\n[5/3] Salvando dados filtrados em: {filteredOutputCsvPath}");
				//parser.SaveAsCsv(filteredRecords, filteredOutputCsvPath, includeDescriptions: false, useDescriptionsInHeaders: true);




				Console.WriteLine("Conversão concluída com sucesso!");

				// Exibir dados do primeiro registro para demonstração
				Console.WriteLine("\nAmostra dos dados do primeiro registro original:");
				Console.WriteLine(records[0]);

				Console.WriteLine("\nAmostra dos dados do primeiro registro filtrado:");
				Console.WriteLine(filteredRecords[0]);

				Console.WriteLine("\nPressione qualquer tecla para sair...");
				Console.ReadKey();



				// Depois de carregar as definições, gerar o schema
				Console.WriteLine("\nGerando schema JSON...");
				var schemaGenerator = new SchemaGenerator(parser.VariableDefinitions, basePath);
				schemaGenerator.GenerateAndSaveSchema();
				Console.WriteLine($"Schema salvo em: {Path.Combine(basePath, "schema.json")}");



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