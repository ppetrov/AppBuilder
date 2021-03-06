﻿using System;
using System.Collections.Generic;
using System.Text;
using AppBuilder.Clr;
using AppBuilder.Db;
using AppBuilder.Db.DDL;

namespace AppBuilder
{
	public sealed class Field
	{
		public string Type { get; private set; }
		public string Name { get; private set; }
		public bool IsDictionary { get; private set; }

		public Field(string type, string name, bool isDictionary = true)
		{
			if (type == null) throw new ArgumentNullException("type");
			if (name == null) throw new ArgumentNullException("name");

			this.Type = type;
			this.Name = name;
			this.IsDictionary = isDictionary;
		}

		public static Field[] GetDictionaryFields(DbTable[] tables, DbTable detailsTable = null)
		{
			if (tables == null) throw new ArgumentNullException("tables");

			var totalFields = tables.Length;
			if (detailsTable != null)
			{
				totalFields++;
			}
			var fields = new Field[totalFields];

			for (var index = 0; index < tables.Length; index++)
			{
				var table = tables[index];
				fields[index] = new Field(table.ClassName, NameProvider.ToParameterName(table.Name));
			}

			if (detailsTable != null)
			{
				fields[fields.Length - 1] = new Field(detailsTable.Name + @"Adapter", @"adapter", false);
			}

			return fields;
		}

		public static Field FindFieldByType(IEnumerable<Field> fields, ClrType type)
		{
			if (fields == null) throw new ArgumentNullException("fields");
			if (type == null) throw new ArgumentNullException("type");

			var typeName = type.Name;

			foreach (var field in fields)
			{
				if (field.Type == typeName)
				{
					return field;
				}
			}

			return null;
		}
	}

	public static class ClrTypeHelper
	{
		public static ClrType GetCollectionType(IEnumerable<ClrProperty> properties)
		{
			if (properties == null) throw new ArgumentNullException("properties");

			foreach (var property in properties)
			{
				var type = property.Type;
				if (type.IsCollection)
				{
					return type;
				}
			}

			return null;
		}
	}

	public static class ForeignKeyHelper
	{
		public static DbTable[] GetForeignKeyTables(IEnumerable<DbColumn> columns, DbSchema schema)
		{
			if (columns == null) throw new ArgumentNullException("columns");
			if (schema == null) throw new ArgumentNullException("schema");

			var foreignKeyTables = new List<DbTable>();

			foreach (var column in columns)
			{
				var foreignKey = column.DbForeignKey;
				if (foreignKey != null)
				{
					var foreignKeyTable = FindTableByForeignKey(schema, foreignKey);
					var collectionType = ClrTypeHelper.GetCollectionType(DbTableConverter.ToClrClass(foreignKeyTable, schema.Tables).Properties);
					if (collectionType == null)
					{
						var name = foreignKeyTable.Name;
						var exists = false;
						foreach (var t in foreignKeyTables)
						{
							if (t.Name == name)
							{
								exists = true;
								break;
							}
						}
						if (!exists)
						{
							foreignKeyTables.Add(foreignKeyTable);
						}
					}
				}
			}

			return foreignKeyTables.ToArray();
		}

		private static DbTable FindTableByForeignKey(DbSchema schema, DbForeignKey foreignKey)
		{
			var foreignKeyTableName = foreignKey.Table;

			foreach (var table in schema.Tables)
			{
				if (table.Name == foreignKeyTableName)
				{
					return table;
				}
			}

			return null;
		}
	}

	public sealed class CodeGenerator
	{
		private readonly StringBuilder _buffer = new StringBuilder(1024);

		public void AddClassDefinition(string className)
		{
			if (className == null) throw new ArgumentNullException("className");

			_buffer.AppendLine(string.Format(@"public sealed class {0}", className));
		}

		public void BeginBlock()
		{
			_buffer.AppendLine(@"{");
		}

		public void EndBlock()
		{
			_buffer.AppendLine(@"}");
		}

		public void EndBlockWith()
		{
			_buffer.AppendLine(@"};");
		}

		public void AddDictionaryFields(Field[] fields)
		{
			if (fields == null) throw new ArgumentNullException("fields");

			foreach (var field in fields)
			{
				_buffer.AppendLine(string.Format("private readonly {0} _{1};", GetDictionaryField(field), field.Name));
			}
		}

		public void AddContructor(Field[] fields, string className)
		{
			if (className == null) throw new ArgumentNullException("className");
			if (fields == null) throw new ArgumentNullException("fields");

			var parameters = new string[fields.Length];
			var parameterChecks = new string[fields.Length];
			var assignemts = new string[fields.Length];

			for (var i = 0; i < fields.Length; i++)
			{
				var field = fields[i];
				var name = field.Name;
				parameters[i] = string.Format(@"{0} {1}", GetDictionaryField(field), name);
				parameterChecks[i] = GetParameterCheck(name);
				assignemts[i] = @"_" + name + @" = " + name + @";";
			}

			_buffer.AppendLine(string.Format(@"public {0}({1})", className, string.Join(@", ", parameters)));

			this.BeginBlock();

			// Append checks
			foreach (var check in parameterChecks)
			{
				_buffer.AppendLine(check);
			}

			this.AddEmptyLine();

			// Append field assignments
			foreach (var assignment in assignemts)
			{
				_buffer.AppendLine(assignment);
			}

			this.EndBlock();
		}

		private string GetDictionaryField(Field field)
		{
			var type = field.Type;
			if (field.IsDictionary)
			{
				return GetDictionaryField(type);
			}
			return type;
		}

		private string GetDictionaryField(string type)
		{
			return string.Format(@"Dictionary<long, {0}>", type);
		}

		private string GetParameterCheck(string name)
		{
			return string.Format(@"if ({0} == null) throw new ArgumentNullException(""{0}"");", name);
		}

		public string GetFormattedOutput()
		{
			var lines = _buffer.ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None);

			var tabs = 0;
			var indentation = string.Empty;

			for (var i = 0; i < lines.Length; i++)
			{
				var value = lines[i];
				if (value == @"{")
				{
					var oldIndentation = indentation;
					tabs++;
					indentation = new string('\t', tabs);
					lines[i] = oldIndentation + value;
					continue;
				}
				if (value == @"}" || value == @"};")
				{
					tabs--;
					indentation = new string('\t', tabs);
				}
				lines[i] = indentation + value;
			}

			return string.Join(Environment.NewLine, lines);
		}

		public void AddEmptyLine()
		{
			_buffer.AppendLine();
		}

		public void AddSelector(string className)
		{
			_buffer.AppendLine(string.Format(@"private long Selector({0} {1}) {{ return {1}.Id; }}", className, char.ToLowerInvariant(className[0])));
		}

		public void AddFillMethod(string className, DbTable table)
		{
			var name = NameProvider.ToParameterName(table.Name);
			_buffer.AppendLine(string.Format(@"public void Fill({0} {1})", GetDictionaryField(className), name));

			this.BeginBlock();

			_buffer.AppendLine(this.GetParameterCheck(name));
			this.AddEmptyLine();
			_buffer.AppendLine(string.Format(@"var query = @""{0}"";", QueryCreator.GetSelect(table).Statement));
			this.AddEmptyLine();
			_buffer.AppendLine(string.Format(@"QueryHelper.Fill({0}, query, this.Creator, this.Selector);", name));

			this.EndBlock();
		}

		public void AddGetMethod(string className, DbTable table)
		{
			_buffer.AppendLine(string.Format(@"public List<{0}> GetAll()", className));

			this.BeginBlock();

			_buffer.AppendLine(string.Format(@"var query = @""{0}"";", QueryCreator.GetSelect(table).Statement));
			this.AddEmptyLine();
			_buffer.AppendLine(@"return QueryHelper.Get(query, this.Creator);");

			this.EndBlock();
		}

		public void AddGetWithDetailsMethod(string className, DbTable table, DbTable detailsTable)
		{
			_buffer.AppendLine(string.Format(@"public List<{0}> GetAll()", className));

			this.BeginBlock();

			_buffer.AppendLine(string.Format(@"var query = @""{0}"";", QueryCreator.GetSelect(table, detailsTable).Statement));
			this.AddEmptyLine();
			_buffer.AppendLine(@"return QueryHelper.Get(query, this.IdReader, this.Creator, _adapter.Creator, this.Attach);");

			this.EndBlock();
		}

		public void AddCreator(ClrClass @class, Field[] fields, int readerIndexOffset = 0)
		{
			var properties = @class.Properties;

			var index = 0;
			var names = new string[properties.Length];
			foreach (var property in properties)
			{
				var name = NameProvider.ToParameterName(property.Name);
				var type = property.Type;
				if (type.IsCollection)
				{
					name = @"new " + type.Name + @"()";
				}
				names[index++] = name;
			}

			Field parameter = null;
			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];
				var name = names[i];
				var type = property.Type;
				var readValue = type.IsBuiltIn || (Field.FindFieldByType(fields, property.Type)) != null;
				if (!readValue && !type.IsCollection)
				{
					parameter = new Field(type.Name, name);
				}
			}

			_buffer.AppendLine(parameter == null
				? string.Format(@"private {0} Creator(IDataReader r)", @class.Name)
				: string.Format(@"public {0} Creator(IDataReader r, {1} {2})", @class.Name, parameter.Type, parameter.Name));

			this.BeginBlock();

			if (parameter != null)
			{
				_buffer.AppendLine(this.GetParameterCheck(@"r"));
				_buffer.AppendLine(this.GetParameterCheck(parameter.Name));
				this.AddEmptyLine();
			}

			var readerIndex = 0;
			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];
				var name = names[i];
				var type = property.Type;

				Field field = null;
				var readValue = type.IsBuiltIn || (field = Field.FindFieldByType(fields, property.Type)) != null;
				if (readValue)
				{
					var value = readerIndex + readerIndexOffset;

					_buffer.AppendLine(string.Format(@"var {0} = {1};", name, type.DefaultValue));
					_buffer.AppendLine(string.Format(@"if (!r.IsDBNull({0}))", value));

					this.BeginBlock();

					if (type.IsBuiltIn)
					{
						_buffer.AppendLine(string.Format(@"{0} = r.{1}({2});", name, type.ReaderMethod, value));
					}
					else
					{
						_buffer.AppendLine(string.Format(@"{0} = _{1}[r.{2}({3})];", name, field.Name, type.ReaderMethod, value));
					}

					this.EndBlock();
					readerIndex++;
				}
			}

			this.AddEmptyLine();

			_buffer.AppendLine(string.Format(@"return new {0}({1});", @class.Name, string.Join(@", ", names)));

			this.EndBlock();
		}

		public void AddInsert(ClrClass @class, DbTable table)
		{
			if (table == null) throw new ArgumentNullException("table");

			var className = table.ClassName;

			var varName = NameProvider.ToParameterName(className);
			_buffer.AppendLine(string.Format(@"public void Insert({0} {1})", className, varName));

			this.BeginBlock();

			_buffer.AppendLine(this.GetParameterCheck(varName));
			this.AddEmptyLine();

			_buffer.AppendLine(string.Format(@"var query = @""{0}"";", QueryCreator.GetInsert(table).Statement));
			this.AddEmptyLine();
			_buffer.AppendLine(@"var sqlParams = new []");

			this.BeginBlock();
			var index = 0;
			var names = QueryCreator.GetParametersWithoutPrimaryKey(table);
			foreach (var property in @class.Properties)
			{
				var type = property.Type;
				if (type.IsCollection)
				{
					continue;
				}
				var name = property.Name;
				if (name != NameProvider.IdName)
				{
					if (!type.IsBuiltIn)
					{
						name += @"." + NameProvider.IdName;
					}
					_buffer.AppendLine(string.Format(@"QueryHelper.Parameter(@""{0}"", {1}.{2}),", names[index++], varName, name));
				}
			}
			this.EndBlockWith();

			this.AddEmptyLine();

			_buffer.AppendLine(@"QueryHelper.ExecuteQuery(query, sqlParams);");
			_buffer.AppendLine(string.Format(@"{0}.Id = Convert.ToInt64(QueryHelper.ExecuteScalar(@""SELECT LAST_INSERT_ROWID()""));", varName));

			this.EndBlock();
		}

		public void AddUpdate(ClrClass @class, DbTable table)
		{
			if (@class == null) throw new ArgumentNullException("class");
			if (table == null) throw new ArgumentNullException("table");

			var varName = NameProvider.ToParameterName(@class.Name);
			_buffer.AppendLine(string.Format(@"public void Update({0} {1})", @class.Name, varName));

			this.BeginBlock();

			_buffer.AppendLine(this.GetParameterCheck(varName));
			this.AddEmptyLine();

			_buffer.AppendLine(string.Format(@"var query = @""{0}"";", QueryCreator.GetUpdate(table).Statement));
			this.AddEmptyLine();
			_buffer.AppendLine(@"var sqlParams = new []");

			this.BeginBlock();
			var index = 0;
			var parameters = QueryCreator.GetParameters(table.Columns);
			foreach (var property in @class.Properties)
			{
				var type = property.Type;
				if (type.IsCollection)
				{
					continue;
				}

				var name = property.Name;
				if (!type.IsBuiltIn)
				{
					name += @"." + NameProvider.IdName;
				}
				_buffer.AppendLine(string.Format(@"QueryHelper.Parameter(@""{0}"", {1}.{2}),", parameters[index++].Name, varName, name));
			}
			this.EndBlockWith();

			this.AddEmptyLine();

			_buffer.AppendLine(@"QueryHelper.ExecuteQuery(query, sqlParams);");

			this.EndBlock();
		}

		public void AddDelete(ClrClass @class, DbTable table)
		{
			if (@class == null) throw new ArgumentNullException("class");
			if (table == null) throw new ArgumentNullException("table");

			var varName = NameProvider.ToParameterName(@class.Name);
			_buffer.AppendLine(string.Format(@"public void Delete({0} {1})", @class.Name, varName));

			this.BeginBlock();

			_buffer.AppendLine(this.GetParameterCheck(varName));
			this.AddEmptyLine();

			_buffer.AppendLine(string.Format(@"var query = @""{0}"";", QueryCreator.GetDelete(table).Statement));
			this.AddEmptyLine();
			_buffer.AppendLine(@"var sqlParams = new []");

			this.BeginBlock();
			_buffer.AppendLine(string.Format(@"QueryHelper.Parameter(@""{0}"", {1}.{0}),", NameProvider.IdName, varName));
			this.EndBlockWith();

			this.AddEmptyLine();

			_buffer.AppendLine(@"QueryHelper.ExecuteQuery(query, sqlParams);");

			this.EndBlock();
		}

		public void AddIdReaderMethod()
		{
			_buffer.AppendLine(string.Format(@"private long IdReader(IDataReader r) {{ return r.GetInt64(0); }}"));
		}

		public void AddAttachMethod(DbTable table, DbTable detailsTable)
		{
			var headerAlias = Convert.ToString(char.ToLowerInvariant(table.ClassName[0]));
			var detailsAlias = Convert.ToString(char.ToLowerInvariant(detailsTable.ClassName[0]));
			if (headerAlias == detailsAlias)
			{
				detailsAlias += @"1";
			}
			_buffer.AppendLine(string.Format(@"private void Attach({0} {1}, {2} {3}) {{ {1}.{4}.Add({3}); }}", table.ClassName, headerAlias, detailsTable.ClassName, detailsAlias, detailsTable.Name));
		}
	}

	public static class AdapterGenerator
	{
		public static string GenerateCode(ClrClass @class, DbTable table, DbSchema schema)
		{
			if (@class == null) throw new ArgumentNullException("class");
			if (table == null) throw new ArgumentNullException("table");
			if (schema == null) throw new ArgumentNullException("schema");

			var foreignKeyTables = ForeignKeyHelper.GetForeignKeyTables(table.Columns, schema);
			if (table.IsReadOnly)
			{
				return GetAdapterReadonOnly(@class, table, foreignKeyTables);
			}
			var collectionType = ClrTypeHelper.GetCollectionType(@class.Properties);
			if (collectionType == null)
			{
				return GetAdapter(@class, table, foreignKeyTables);
			}
			return GetAdapterWithCollection(@class, table, foreignKeyTables, FindCollectionTable(schema, collectionType));
		}

		private static string GetAdapterReadonOnly(ClrClass @class, DbTable table, DbTable[] foreignKeyTables)
		{
			var generator = new CodeGenerator();

			var className = AddClassDefinition(table, generator);

			generator.BeginBlock();
			var fields = AddFiledsAndContructor(generator, className, foreignKeyTables);

			// Add Fill method
			generator.AddFillMethod(@class.Name, table);
			generator.AddEmptyLine();

			// Add Creator
			generator.AddCreator(@class, fields);
			generator.AddEmptyLine();

			// Add Selector
			generator.AddSelector(@class.Name);

			generator.EndBlock();

			return generator.GetFormattedOutput();
		}

		private static string GetAdapter(ClrClass @class, DbTable table, DbTable[] foreignKeyTables)
		{
			var generator = new CodeGenerator();

			var className = AddClassDefinition(table, generator);
			generator.BeginBlock();

			var fields = AddFiledsAndContructor(generator, className, foreignKeyTables);

			var addGetMethod = true;
			foreach (var property in @class.Properties)
			{
				var type = property.Type;
				if (!type.IsBuiltIn)
				{
					if (!HasForeignKeyTableFor(foreignKeyTables, type))
					{
						addGetMethod = false;
						break;
					}
				}
			}

			if (addGetMethod)
			{
				// Add Get method
				generator.AddGetMethod(@class.Name, table);
				generator.AddEmptyLine();
			}

			// Add Creator
			generator.AddCreator(@class, fields);
			generator.AddEmptyLine();

			AddInsertUpdateDelete(generator, @class, table);

			generator.EndBlock();

			return generator.GetFormattedOutput();
		}

		private static string GetAdapterWithCollection(ClrClass @class, DbTable table, DbTable[] foreignKeyTables, DbTable detailsTable)
		{
			var generator = new CodeGenerator();
			var className = AddClassDefinition(table, generator);

			generator.BeginBlock();
			var fields = AddFiledsAndContructor(generator, className, foreignKeyTables, detailsTable);

			// Add Get method
			generator.AddGetWithDetailsMethod(@class.Name, table, detailsTable);
			generator.AddEmptyLine();

			// Add IdReader method
			generator.AddIdReaderMethod();
			generator.AddEmptyLine();

			// Add Attach method
			generator.AddAttachMethod(table, detailsTable);
			generator.AddEmptyLine();

			// Add Creator
			generator.AddCreator(@class, fields, detailsTable.Columns.Length - 1);
			generator.AddEmptyLine();

			AddInsertUpdateDelete(generator, @class, table);

			generator.EndBlock();

			return generator.GetFormattedOutput();
		}

		private static string AddClassDefinition(DbTable table, CodeGenerator generator)
		{
			var className = string.Format(@"{0}Adapter", table.Name);

			generator.AddClassDefinition(className);

			return className;
		}

		private static Field[] AddFiledsAndContructor(CodeGenerator generator, string className, DbTable[] foreignKeyTables, DbTable detailsTable = null)
		{
			var fields = Field.GetDictionaryFields(foreignKeyTables, detailsTable);
			if (fields.Length > 0)
			{
				// Add fields
				generator.AddDictionaryFields(fields);
				generator.AddEmptyLine();

				// Add contructor
				generator.AddContructor(fields, className);
				generator.AddEmptyLine();
			}
			return fields;
		}

		private static void AddInsertUpdateDelete(CodeGenerator generator, ClrClass @class, DbTable table)
		{
			// Add Insert
			generator.AddInsert(@class, table);
			generator.AddEmptyLine();

			// Add Update
			generator.AddUpdate(@class, table);
			generator.AddEmptyLine();

			// Add Delete
			generator.AddDelete(@class, table);
		}

		private static bool HasForeignKeyTableFor(DbTable[] foreignKeyTables, ClrType type)
		{
			var name = type.Name;

			foreach (var t in foreignKeyTables)
			{
				if (t.ClassName == name)
				{
					return true;
				}
			}

			return false;
		}

		private static DbTable FindCollectionTable(DbSchema schema, ClrType collectionType)
		{
			var typeName = collectionType.Name;
			foreach (var t in schema.Tables)
			{
				if (ClrType.GetUserCollectionTypeName(t.ClassName) == typeName)
				{
					return t;
				}
			}

			return null;
		}
	}
}