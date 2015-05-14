﻿using System;
using System.Collections.Generic;
using System.Text;
using AppBuilder.Clr;
using AppBuilder.Db;
using AppBuilder.Db.Providers;

namespace AppBuilder
{
	public static class CodeGenerator
	{
		private static readonly char Space = ' ';
		private static readonly char Comma = ',';
		private static readonly char Semicolumn = ';';

		public static string GetClass(ClrClass @class, bool immutable)
		{
			if (@class == null) throw new ArgumentNullException("class");

			var buffer = new StringBuilder(1024);

			buffer.Append(@"public");
			buffer.Append(Space);
			var @sealed = GetIsSealed(@class.Sealed);
			if (@sealed != string.Empty)
			{
				buffer.Append(@sealed);
				buffer.Append(Space);
			}
			buffer.Append(@"class");
			buffer.Append(Space);
			buffer.Append(@class.Name);
			var interfaces = @class.Interfaces;
			if (interfaces.Count > 0)
			{
				buffer.Append(@" : ");

				var addSeparator = false;
				foreach (var @interface in interfaces)
				{
					if (addSeparator)
					{
						buffer.Append(Comma);
					}
					buffer.Append(@interface);
					addSeparator = true;
				}
			}
			buffer.AppendLine();

			buffer.AppendLine(@"{");

			if (@class.Fields.Count > 0)
			{
				foreach (var field in @class.Fields)
				{
					buffer.AppendLine(GetField(field));
				}
				buffer.AppendLine();
			}
			var properties = @class.Properties;
			if (properties.Length > 0)
			{
				foreach (var property in properties)
				{
					buffer.AppendLine(GetProperty(property, immutable));
				}
				buffer.AppendLine();
			}

			ClrContructor contructor;
			if (immutable)
			{
				contructor = new ClrContructor(@class.Name, GetParameters(properties));
			}
			else
			{
				contructor = new ClrContructor(@class.Name, properties);
			}
			buffer.AppendLine(GetContructor(contructor));
			buffer.AppendLine(@"}");

			return buffer.ToString();
		}

		public static string GetHelper(ClrClass @class, NameProvider nameProvider)
		{
			if (nameProvider == null) throw new ArgumentNullException("nameProvider");
			if (@class == null) throw new ArgumentNullException("class");

			var buffer = new StringBuilder(1024);
			var name = @class.Name;
			var varName = StringUtils.LowerFirst(nameProvider.GetDbName(name));

			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(@"sealed class");
			buffer.Append(Space);
			buffer.Append(name);
			buffer.Append(@"Helper");
			buffer.AppendLine(@"{");

			// Field
			buffer.Append(@"private readonly");
			buffer.Append(Space);
			buffer.Append(@"Dictionary<long, ");
			buffer.Append(name);
			buffer.Append(@"> _");
			buffer.Append(varName);
			buffer.Append(@" = new ");
			buffer.Append(@"Dictionary<long, ");
			buffer.Append(name);
			buffer.AppendLine(@">();");

			// Property
			buffer.AppendLine();
			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(@"Dictionary<long, ");
			buffer.Append(name);
			buffer.Append(@"> ");
			buffer.Append(varName);
			StringUtils.UpperFirst(buffer, varName);
			buffer.AppendLine();
			buffer.AppendLine(@"{");
			buffer.Append(@"get { return _");
			buffer.Append(varName);
			buffer.Append(@"; }");
			buffer.AppendLine(@"}");
			buffer.AppendLine();

			// Method
			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(@"void Load(");
			buffer.Append(name);
			buffer.Append(@"Adapter adapter)");
			buffer.AppendLine(@"{");
			buffer.AppendLine(@"if (adapter == null) throw new ArgumentNullException(""adapter"");");
			buffer.AppendLine();
			buffer.Append(@"adapter.Fill(_");
			buffer.Append(varName);
			buffer.AppendLine(@");");
			buffer.AppendLine(@"}");
			buffer.AppendLine(@"}");

			return buffer.ToString();
		}

		public static string GetAdapterInterface(ClrClass @class, NameProvider nameProvider)
		{
			if (@class == null) throw new ArgumentNullException("class");
			if (nameProvider == null) throw new ArgumentNullException("nameProvider");

			var buffer = new StringBuilder(@"public interface I", 128);

			buffer.Append(@class.Name);
			buffer.Append(@"Adapter");
			buffer.AppendLine(@"{");
			buffer.Append(@"void Fill(");
			buffer.Append(@"Dictionary<long, ");
			buffer.Append(@class.Name);
			buffer.Append(@"> ");
			buffer.Append(StringUtils.LowerFirst(nameProvider.GetDbName(@class.Name)));
			buffer.Append(@");");
			buffer.AppendLine(@"}");

			return buffer.ToString();
		}

		public static string GetAdapter(ClrClass @class, NameProvider nameProvider, bool readOnly, DbTable table)
		{
			if (@class == null) throw new ArgumentNullException("class");
			if (nameProvider == null) throw new ArgumentNullException("nameProvider");
			if (table == null) throw new ArgumentNullException("table");

			var buffer = new StringBuilder(2 * 1024);
			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(@"sealed class");
			buffer.Append(Space);
			buffer.Append(@class.Name);
			buffer.Append(@"Adapter");
			buffer.Append(Space);
			buffer.Append(':');
			buffer.Append(Space);
			buffer.Append('I');
			buffer.Append(@class.Name);
			buffer.Append(@"Adapter");
			buffer.AppendLine(@"{");
			var fields = GetFileds(@class, nameProvider);
			if (fields.Length > 0)
			{
				AppendConstructor(buffer, @class, fields);
			}
			AppendFillMethod(buffer, @class, QueryProvider.GetSelect(table));
			AppendCreatorMethod(buffer, @class, readOnly, fields);
			AppendSelectorMethod(buffer, @class);
			buffer.AppendLine(@"}");

			return buffer.ToString();
		}

		private static ClrField[] GetFileds(ClrClass @class, NameProvider nameProvider)
		{
			var totalFields = 0;
			foreach (var property in @class.Properties)
			{
				var type = property.Type;
				if (!type.IsBuiltIn)
				{
					totalFields++;
				}
			}

			var fields = new ClrField[totalFields];
			if (fields.Length > 0)
			{
				var i = 0;
				foreach (var property in @class.Properties)
				{
					var type = property.Type;
					if (!type.IsBuiltIn)
					{
						fields[i++] = new ClrField(type, nameProvider.GetDbName(type.Name), property: property);
					}
				}
			}
			return fields;
		}

		private static void AppendConstructor(StringBuilder buffer, ClrClass @class, ClrField[] fields)
		{
			foreach (var field in fields)
			{
				AppendField(buffer, field);
			}
			buffer.AppendLine();

			buffer.Append(@"public ");
			buffer.Append(@class.Name);
			buffer.Append(@"Adapter(");
			var parameters = new ClrParameter[fields.Length];
			for (var i = 0; i < fields.Length; i++)
			{
				var field = fields[i];
				parameters[i] = new ClrParameter(field.Type, field.Name.Substring(1));
			}
			foreach (var parameter in parameters)
			{
				AppendDictionaryParameter(buffer, parameter);
			}
			buffer[buffer.Length - 1] = ')';
			buffer.AppendLine(@"{");
			foreach (var parameter in parameters)
			{
				AppendCheck(buffer, parameter.Name);
			}
			buffer.AppendLine();
			for (var i = 0; i < fields.Length; i++)
			{
				AppendFieldAssignment(buffer, fields[i], parameters[i]);
			}
			buffer.AppendLine(@"}");
			buffer.AppendLine();
		}

		private static void AppendField(StringBuilder buffer, ClrField field)
		{
			buffer.Append(@"private readonly");
			buffer.Append(Space);
			AppendDictionary(buffer, field.Type, field.Name);
			buffer.Append(Semicolumn);
			buffer.AppendLine();
		}

		private static void AppendDictionary(StringBuilder buffer, ClrType type, string name)
		{
			buffer.Append(@"Dictionary<long, ");
			buffer.Append(type.Name);
			buffer.Append(@"> ");
			buffer.Append(name);
		}

		private static void AppendDictionaryParameter(StringBuilder buffer, ClrParameter parameter)
		{
			AppendDictionary(buffer, parameter.Type, parameter.Name);
			buffer.Append(Comma);
		}

		private static void AppendFieldAssignment(StringBuilder buffer, ClrField field, ClrParameter parameter)
		{
			buffer.Append(field.Name);
			buffer.Append(@" = ");
			buffer.Append(parameter.Name);
			buffer.Append(Semicolumn);
			buffer.AppendLine();
		}

		private static void AppendFillMethod(StringBuilder buffer, ClrClass @class, string query)
		{
			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(@"void Fill(");
			buffer.Append(@"Dictionary<long, ");
			buffer.Append(@class.Name);
			buffer.Append(@"> items)");
			buffer.AppendLine(@"{");
			AppendCheck(buffer, @"items");
			buffer.AppendLine();
			buffer.Append(@"var query = """);
			buffer.Append(query);
			buffer.AppendLine(@""";");
			buffer.AppendLine(@"QueryHelper.Fill(items, query, this.Creator, this.Selector);");
			buffer.AppendLine(@"}");
			buffer.AppendLine();
		}

		private static void AppendCreatorMethod(StringBuilder buffer, ClrClass @class, bool readOnly, ClrField[] fields)
		{
			var name = @class.Name;
			buffer.Append(@"private");
			buffer.Append(Space);
			buffer.Append(name);
			buffer.AppendLine(@" Creator(IDataReader r)");
			buffer.AppendLine(@"{");

			var properties = @class.Properties;
			var parameters = GetParameters(properties);
			for (var i = 0; i < properties.Length; i++)
			{
				var property = properties[i];
				var parameter = parameters[i];

				buffer.Append(@"var ");
				buffer.Append(parameter.Name);
				buffer.Append(@" = ");
				buffer.Append(property.Type.DefaultValue);
				buffer.Append(Semicolumn);
				buffer.AppendLine();
				buffer.Append(@"if (!r.IsDBNull(");
				buffer.Append(i);
				buffer.Append(@")");
				buffer.Append(@")");
				buffer.AppendLine(@"{");
				buffer.Append(parameter.Name);
				buffer.Append(@" = ");
				AppendReaderGetValue(buffer, property, i, fields);
				buffer.Append(Semicolumn);
				buffer.AppendLine(@"}");
			}

			buffer.Append(@"return new ");
			buffer.Append(name);
			if (readOnly)
			{
				buffer.Append(@"(");
				foreach (var parameter in parameters)
				{
					buffer.Append(parameter.Name);
					buffer.Append(Comma);
				}
				buffer[buffer.Length - 1] = ')';
			}
			else
			{
				buffer.Append(@"{");
				for (var i = 0; i < properties.Length; i++)
				{
					buffer.Append(properties[i].Name);
					buffer.Append(@" = ");
					buffer.Append(parameters[i].Name);
					buffer.Append(Comma);
				}
				buffer[buffer.Length - 1] = '}';
			}
			buffer.AppendLine(@";");
			buffer.AppendLine(@"}");
		}

		private static void AppendReaderGetValue(StringBuilder buffer, ClrProperty property, int index, IEnumerable<ClrField> fields)
		{
			var type = property.Type;
			if (type.IsBuiltIn)
			{
				AppendReaderGetValue(buffer, type, index);
			}
			else
			{
				foreach (var field in fields)
				{
					if (field.Property == property)
					{
						buffer.Append(field.Name);
						break;
					}
				}
				buffer.Append(@"[");
				AppendReaderGetValue(buffer, ClrType.Long, index);
				buffer.Append(@"]");
			}
		}

		private static void AppendReaderGetValue(StringBuilder buffer, ClrType type, int index)
		{
			buffer.Append(@"r.");
			buffer.Append(type.ReaderMethod);
			buffer.Append(@"(");
			buffer.Append(index);
			buffer.Append(@")");
		}

		private static void AppendSelectorMethod(StringBuilder buffer, ClrClass @class)
		{
			buffer.AppendLine();

			var name = @class.Name;
			var varName = char.ToLowerInvariant(name[0]);
			var primaryKeyProperty = default(ClrProperty);
			foreach (var property in @class.Properties)
			{
				if (property.Type == ClrType.Long && property.Name.EndsWith(@"Id"))
				{
					primaryKeyProperty = property;
					break;
				}
			}

			buffer.Append(@"private long Selector(");
			buffer.Append(name);
			buffer.Append(Space);
			buffer.Append(varName);
			buffer.Append(@") { return ");
			buffer.Append(varName);
			buffer.Append(@".");
			buffer.Append(primaryKeyProperty.Name);
			buffer.Append(@";}");
			buffer.AppendLine();
		}

		private static string GetIsReadOnly(bool isReadOnly)
		{
			return isReadOnly ? @"readonly" : string.Empty;
		}

		private static string GetIsSealed(bool isSealed)
		{
			return isSealed ? @"sealed" : string.Empty;
		}

		private static string GetField(ClrField field)
		{
			if (field == null) throw new ArgumentNullException("field");

			var buffer = new StringBuilder();

			buffer.Append(@"private");
			buffer.Append(Space);
			AppendReadOnly(field, buffer);
			buffer.Append(field.Type.Name);
			buffer.Append(Space);
			buffer.Append(field.Name);
			AppendInitialValue(field, buffer);
			buffer.Append(Semicolumn);

			return buffer.ToString();
		}

		private static void AppendInitialValue(ClrField field, StringBuilder buffer)
		{
			var initialValue = field.InitialValue;
			if (initialValue != string.Empty)
			{
				buffer.Append(@" = ");
				buffer.Append(initialValue);
			}
		}

		private static void AppendReadOnly(ClrField field, StringBuilder buffer)
		{
			var readOnly = GetIsReadOnly(field.IsReadOnly);
			if (readOnly != string.Empty)
			{
				buffer.Append(readOnly);
				buffer.Append(Space);
			}
		}

		private static string GetProperty(ClrProperty property, bool immutable)
		{
			if (property == null) throw new ArgumentNullException("property");

			var buffer = new StringBuilder();

			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(property.Type.Name);
			buffer.Append(Space);
			buffer.Append(property.Name);
			buffer.Append(Space);
			buffer.Append(GetBackingField(immutable));

			return buffer.ToString();
		}

		private static string GetContructor(ClrContructor definition)
		{
			var buffer = new StringBuilder();

			buffer.Append(@"public");
			buffer.Append(Space);
			buffer.Append(definition.Name);
			buffer.Append('(');
			var addSeparator = false;
			foreach (var parameter in definition.Parameters)
			{
				if (addSeparator)
				{
					buffer.Append(Comma);
				}
				AppendParameter(buffer, parameter);
				addSeparator = true;
			}
			buffer.Append(')');
			buffer.AppendLine();
			buffer.AppendLine(@"{");

			var hasChecks = false;
			foreach (var parameter in definition.Parameters)
			{
				if (parameter.Type.IsReference)
				{
					AppendCheck(buffer, parameter);
					hasChecks = true;
				}
			}
			if (hasChecks)
			{
				buffer.AppendLine();
			}
			foreach (var parameter in definition.Parameters)
			{
				AppendPropertyAssignment(buffer, parameter);
			}
			foreach (var parameter in definition.Properties)
			{
				AppendPropertyInitialization(buffer, parameter);
			}
			buffer.AppendLine(@"}");

			return buffer.ToString();
		}

		private static void AppendPropertyAssignment(StringBuilder buffer, ClrParameter parameter)
		{
			var name = parameter.Name;
			var upper = char.ToUpperInvariant(name[0]);
			buffer.Append(@"this.");
			buffer.Append(name);
			buffer[buffer.Length - name.Length] = upper;
			buffer.Append(@" = ");
			buffer.Append(name);
			buffer.AppendLine(@";");
		}

		private static void AppendPropertyInitialization(StringBuilder buffer, ClrProperty parameter)
		{
			buffer.Append(@"this.");
			buffer.Append(parameter.Name);
			buffer.Append(@" = ");
			buffer.Append(parameter.Type.DefaultValue);
			buffer.AppendLine(@";");
		}

		private static void AppendCheck(StringBuilder buffer, ClrParameter parameter)
		{
			AppendCheck(buffer, parameter.Name);
		}

		private static void AppendCheck(StringBuilder buffer, string name)
		{
			buffer.Append(@"if (");
			buffer.Append(name);
			buffer.Append(@" == null) throw new ArgumentNullException(""");
			buffer.Append(name);
			buffer.Append(@""");");
			buffer.AppendLine();
		}

		private static void AppendParameter(StringBuilder buffer, ClrParameter parameter)
		{
			var type = parameter.Type.Name;
			var name = parameter.Name;

			buffer.Append(type);
			buffer.Append(Space);
			buffer.Append(name);
		}

		private static string GetBackingField(bool immutable)
		{
			return immutable ? @"{ get; private set; }" : @"{ get; set; }";
		}

		private static ClrParameter[] GetParameters(IReadOnlyList<ClrProperty> properties)
		{
			var parameters = new ClrParameter[properties.Count];
			for (var i = 0; i < properties.Count; i++)
			{
				var property = properties[i];
				parameters[i] = new ClrParameter(property.Type, property.Name);
			}
			return parameters;
		}
	}
}