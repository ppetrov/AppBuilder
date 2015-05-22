using System;
using System.Collections.Generic;

namespace AppBuilder.Clr
{
	public sealed class ClrClass
	{
		public string Name { get; private set; }
		public List<string> Interfaces { get; private set; }
		public ClrProperty[] Properties { get; private set; }

		public ClrClass(string name, ClrProperty[] properties)
		{
			if (name == null) throw new ArgumentNullException("name");
			if (properties == null) throw new ArgumentNullException("properties");
			if (properties.Length == 0) throw new ArgumentOutOfRangeException("properties");

			this.Name = NameProvider.ToClassName(name);
			this.Interfaces = new List<string>();
			this.Properties = properties;
		}
	}
}