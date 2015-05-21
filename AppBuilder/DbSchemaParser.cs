﻿using System;
using System.Collections.Generic;
using System.Text;
using AppBuilder.Db;
using AppBuilder.Db.DDL;

namespace AppBuilder
{
	public static class DbSchemaParser
	{
		public static DbTable[] ParseTables(string script)
		{
			if (script == null) throw new ArgumentNullException("script");

			var schemas = script.Trim().Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
			var tables = new DbTable[schemas.Length];
			for (var i = 0; i < schemas.Length; i++)
			{
				tables[i] = Parse(schemas[i]);
			}
			return tables;
		}

		private static DbTable Parse(string tableSchema)
		{
			var input = StringUtils.NormalizeTableSchema(tableSchema);
			var tableName = StringUtils.ExtractBetween(input, @"CREATE TABLE", @"(").Trim();

			var columns = new List<DbColumn>();
			foreach (var definition in GetColumnDefinitions(StringUtils.ExtractBetweenGreedy(input, @"(", @")")))
			{
				var value = definition.Trim();
				var columnName = StringUtils.ExtractBetween(value, @"FOREIGN KEY", @")");
				if (columnName != string.Empty)
				{
					columnName = columnName.Trim().Substring(1);

					var foreignKey = ParseForeignKey(value);
					for (var i = 0; i < columns.Count; i++)
					{
						var column = columns[i];
						if (column.Name == columnName)
						{
							columns[i] = new DbColumn(column.Type, column.Name, foreignKey, column.AllowNull, column.IsPrimaryKey);
							break;
						}
					}
				}
				else
				{
					columns.Add(ParseColumn(value));
				}
			}

			return new DbTable(tableName, columns.ToArray());
		}

		private static IEnumerable<string> GetColumnDefinitions(string input)
		{
			const char separator = ',';

			var buffer = new StringBuilder(256);
			foreach (var symbol in input)
			{
				if (symbol == separator)
				{
					var current = buffer.ToString();
					if ((current.IndexOf(@"DECIMAL(", StringComparison.OrdinalIgnoreCase) >= 0 ||
						current.IndexOf(@"NUMERIC(", StringComparison.OrdinalIgnoreCase) >= 0) &&
						current.IndexOf(separator) < 0)
					{
						buffer.Append(symbol);
						continue;
					}
					yield return current.Trim();
					buffer = new StringBuilder();
				}
				else
				{
					buffer.Append(symbol);
				}
			}

			if (buffer.Length > 0)
			{
				yield return buffer.ToString().Trim();
			}
		}

		private static DbForeignKey ParseForeignKey(string input)
		{
			var value = StringUtils.ExtractBetween(input, @"REFERENCES ", @")");
			var index = value.IndexOf('(');
			var table = value.Substring(0, index).Trim();
			var column = value.Substring(index + 1).Trim();
			return new DbForeignKey(StringUtils.UpperFirst(table), StringUtils.UpperFirst(column));
		}

		private static DbColumn ParseColumn(string input)
		{
			var name = input.Substring(0, input.IndexOf(' '));
			var type = ParseColumnType(StringUtils.ExtractBetween(input, @" ", @" "));
			var allowNull = input.IndexOf(@"NOT NULL", StringComparison.OrdinalIgnoreCase) < 0;
			var isPrimaryKey = input.IndexOf(@"PRIMARY KEY", StringComparison.OrdinalIgnoreCase) >= 0;
			return new DbColumn(type, name, allowNull, isPrimaryKey);
		}

		private static DbColumnType ParseColumnType(string input)
		{
			if (input.Equals(@"INTEGER", StringComparison.OrdinalIgnoreCase))
			{
				return DbColumnType.Integer;
			}
			if (input.StartsWith(@"TEXT", StringComparison.OrdinalIgnoreCase))
			{
				var length = default(int?);
				var value = StringUtils.ExtractBetween(input, @"(", @")");
				if (value != string.Empty)
				{
					int number;
					if (int.TryParse(value, out number))
					{
						length = number;
					}
				}
				return DbColumnType.GetString(length);
			}
			if (input.Equals(@"BLOB", StringComparison.OrdinalIgnoreCase))
			{
				return DbColumnType.Bytes;
			}
			if (input.StartsWith(@"DECIMAL", StringComparison.OrdinalIgnoreCase) || input.StartsWith(@"NUMERIC", StringComparison.OrdinalIgnoreCase))
			{
				return DbColumnType.Decimal;
			}
			if (input.StartsWith(@"DATETIME", StringComparison.OrdinalIgnoreCase))
			{
				return DbColumnType.DateTime;
			}
			throw new ArgumentOutOfRangeException(@"input");
		}
	}
}