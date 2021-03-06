using System;

namespace AppBuilder.Clr
{
	public sealed class ClrType
	{
		public static readonly ClrType Long = new ClrType(@"long", false, @"0L", @"GetInt64");
		public static readonly ClrType Decimal = new ClrType(@"decimal", false, @"0M", @"GetDecimal");
		public static readonly ClrType String = new ClrType(@"string", true, @"string.Empty", @"GetString");
		public static readonly ClrType DateTime = new ClrType(@"DateTime", false, @"DateTime.MinValue", @"GetDateTime");
		public static readonly ClrType Bytes = new ClrType(@"byte[]", false, @"default(byte[])", @"GetBytes");

		public string Name { get; private set; }
		public bool CheckValue { get; private set; }
		public bool IsBuiltIn { get; private set; }
		public bool IsCollection { get; private set; }
		public string DefaultValue { get; private set; }
		public string ReaderMethod { get; private set; }

		private ClrType(string name, bool checkValue, bool isCollection)
		{
			if (name == null) throw new ArgumentNullException("name");

			this.Name = name;
			this.CheckValue = checkValue;
			this.DefaultValue = string.Format(@"default({0})", name);
			this.ReaderMethod = @"GetInt64";
			this.IsCollection = isCollection;
		}

		private ClrType(string name, bool isReference, string defaultValue, string readerMethod)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (defaultValue == null) throw new ArgumentNullException("defaultValue");
			if (readerMethod == null) throw new ArgumentNullException("readerMethod");

			this.Name = name;
			this.CheckValue = isReference;
			this.IsBuiltIn = true;
			this.DefaultValue = defaultValue;
			this.ReaderMethod = readerMethod;
		}

		public static ClrType UserType(string name, bool checkValue = true)
		{
			if (name == null) throw new ArgumentNullException("name");

			return new ClrType(name, checkValue, false);
		}

		public static ClrType UserCollection(string name)
		{
			if (name == null) throw new ArgumentNullException("name");

			return new ClrType(GetUserCollectionTypeName(name), true, true);
		}

		public static string GetUserCollectionTypeName(string name)
		{
			if (name == null) throw new ArgumentNullException("name");

			return string.Format(@"List<{0}>", name);
		}
	}
}