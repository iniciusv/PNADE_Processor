using System;
using System.Collections.Generic;
using System.Linq;

public class DataProcessor
{
	private List<FixedWidthRecord> _records;

	public DataProcessor(List<FixedWidthRecord> records)
	{
		_records = records ?? throw new ArgumentNullException(nameof(records));
	}

	public List<FixedWidthRecord> FilterRecords(List<FixedWidthRecord> recordsToFilter, List<string> parameters)
	{
		if (recordsToFilter == null)
			throw new ArgumentNullException(nameof(recordsToFilter));

		if (parameters == null || !parameters.Any())
			return recordsToFilter;

		var filteredRecords = new List<FixedWidthRecord>();

		foreach (var record in recordsToFilter)
		{
			var filteredRecord = new FixedWidthRecord();

			foreach (var param in parameters)
			{
				if (record.Fields.TryGetValue(param, out var fieldData))
				{
					filteredRecord.Fields[param] = fieldData;
				}
			}

			if (filteredRecord.Fields.Any())
			{
				filteredRecords.Add(filteredRecord);
			}
		}

		return filteredRecords;
	}

	// Método de conveniência que usa os records da instância
	public List<FixedWidthRecord> FilterRecords(List<string> parameters)
	{
		return FilterRecords(_records, parameters);
	}
}