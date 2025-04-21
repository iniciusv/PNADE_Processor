// Adicione esta classe ao seu projeto
using System.Text.Json.Serialization;
using System.Text.Json;

public class SchemaGenerator
{
	private readonly List<VariableDefinition> _variableDefinitions;
	private readonly string _basePath;

	public SchemaGenerator(List<VariableDefinition> variableDefinitions, string basePath)
	{
		_variableDefinitions = variableDefinitions ?? throw new ArgumentNullException(nameof(variableDefinitions));
		_basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
	}

	public void GenerateAndSaveSchema()
	{
		var schema = new JsonSchema
		{
			Title = "Fixed Width File Schema",
			Description = "Schema generated from variable definitions",
			Type = "object",
			Properties = new Dictionary<string, JsonSchemaProperty>(),
			Required = new List<string>()
		};

		foreach (var varDef in _variableDefinitions)
		{
			var property = new JsonSchemaProperty
			{
				Description = varDef.Description,
				Type = InferJsonType(varDef),
				Position = varDef.StartPosition,
				Length = varDef.Length
			};

			if (varDef.ValueLabels.Count > 0)
			{
				property.Enum = varDef.ValueLabels.Keys.ToList();
				property.EnumDescriptions = varDef.ValueLabels.Values.ToList();
			}

			schema.Properties[varDef.VariableCode] = property;
			schema.Required.Add(varDef.VariableCode);
		}

		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		};

		string schemaJson = JsonSerializer.Serialize(schema, options);
		string schemaPath = Path.Combine(_basePath, "schema.json");
		File.WriteAllText(schemaPath, schemaJson);
	}


private string InferJsonType(VariableDefinition varDef)
	{
		if (varDef.ValueLabels.Count > 0)
		{
			var sampleValue = varDef.ValueLabels.Keys.First();
			if (int.TryParse(sampleValue, out _))
				return "integer";
			if (double.TryParse(sampleValue, out _))
				return "number";
		}
		return "string";
	}
}

// Classes auxiliares para o schema JSON
public class JsonSchema
{
	[JsonPropertyName("$schema")]
	public string Schema { get; set; } = "https://json-schema.org/draft/2020-12/schema";
	public string Title { get; set; }
	public string Description { get; set; }
	public string Type { get; set; }
	public Dictionary<string, JsonSchemaProperty> Properties { get; set; }
	public List<string> Required { get; set; }
}

public class JsonSchemaProperty
{
	public string Description { get; set; }
	public string Type { get; set; }
	public int Position { get; set; }
	public int Length { get; set; }
	public List<string> Enum { get; set; }
	public List<string> EnumDescriptions { get; set; }
}