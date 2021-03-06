﻿namespace AppBuilder
{
	public static class QueryHelperGenerator
	{
		public static string GetCode()
		{
			return @"	public static class QueryHelper
	{
		public static IDbConnection Connection { get; set; }
		public static Func<string, object, IDbDataParameter> ParameterCreator;

		public static IDbDataParameter Parameter(string name, object value)
		{
			if (name == null) throw new ArgumentNullException(""name"");

			return ParameterCreator(name, value ?? DBNull.Value);
		}

		public static object ExecuteScalar(string query)
		{
			using (var cmd = Connection.CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = query;

				return cmd.ExecuteScalar();
			}
		}

		public static int ExecuteQuery(string query)
		{
			return ExecuteQuery(query, new IDbDataParameter[0]);
		}

		public static int ExecuteQuery(string query, IEnumerable<IDbDataParameter> parameters)
		{
			if (query == null) throw new ArgumentNullException(""query"");
			if (parameters == null) throw new ArgumentNullException(""parameters"");

			int total;

			using (var cmd = Connection.CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = query;

				foreach (var p in parameters)
				{
					cmd.Parameters.Add(p);
				}

				total = cmd.ExecuteNonQuery();
			}

			return total;
		}

		public static List<T> Get<T>(string query, Func<IDataReader, T> creator)
		{
			return Get(query, creator, new IDbDataParameter[0]);
		}

		public static List<T> Get<T>(string query, Func<IDataReader, T> creator, IEnumerable<IDbDataParameter> parameters)
		{
			return Get(query, creator, parameters, 4);
		}

		public static List<T> Get<T>(string query, Func<IDataReader, T> creator, int capacity)
		{
			return Get(query, creator, new IDbDataParameter[0], capacity);
		}

		public static List<T> Get<T>(string query, Func<IDataReader, T> creator, IEnumerable<IDbDataParameter> parameters, int capacity)
		{
			if (query == null) throw new ArgumentNullException(""query"");
			if (creator == null) throw new ArgumentNullException(""creator"");
			if (parameters == null) throw new ArgumentNullException(""parameters"");

			var items = new List<T>(capacity);
			using (var cmd = Connection.CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = query;

				foreach (var p in parameters)
				{
					cmd.Parameters.Add(p);
				}

				using (var reader = cmd.ExecuteReader())
				{
					while (reader.Read())
					{
						var v = creator(reader);
						items.Add(v);
					}
				}
			}

			return items;
		}

		public static void Fill<T>(Dictionary<long, T> items, string query, Func<IDataReader, T> creator, Func<T, long> selector)
		{
			if (items == null) throw new ArgumentNullException(""items"");
			if (query == null) throw new ArgumentNullException(""query"");
			if (creator == null) throw new ArgumentNullException(""creator"");
			if (selector == null) throw new ArgumentNullException(""selector"");

			items.Clear();

			using (var cmd = Connection.CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = query;

				using (var r = cmd.ExecuteReader())
				{
					while (r.Read())
					{
						var item = creator(r);
						items.Add(selector(item), item);
					}
				}
			}
		}

		public static List<THeader> Get<THeader, TDetail>(string query, Func<IDataReader, long> idReader, Func<IDataReader, THeader> headerCreator, Func<IDataReader, THeader, TDetail> detailCreator, Action<THeader, TDetail> attach)
			where THeader : class
			where TDetail : class
		{
			return Get(query, idReader, headerCreator, detailCreator, attach, new IDbDataParameter[0]);
		}

		public static List<THeader> Get<THeader, TDetail>(string query, Func<IDataReader, long> idReader, Func<IDataReader, THeader> headerCreator, Func<IDataReader, THeader, TDetail> detailCreator, Action<THeader, TDetail> attach, IEnumerable<IDbDataParameter> parameters)
			where THeader : class
			where TDetail : class
		{
			if (query == null) throw new ArgumentNullException(""query"");
			if (idReader == null) throw new ArgumentNullException(""idReader"");
			if (headerCreator == null) throw new ArgumentNullException(""headerCreator"");
			if (detailCreator == null) throw new ArgumentNullException(""detailCreator"");
			if (attach == null) throw new ArgumentNullException(""attach"");
			if (parameters == null) throw new ArgumentNullException(""parameters"");

			var items = new Dictionary<long, THeader>();

			using (var cmd = Connection.CreateCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.CommandText = query;

				using (var r = cmd.ExecuteReader())
				{
					while (r.Read())
					{
						var id = idReader(r);

						THeader header;
						if (!items.TryGetValue(id, out header))
						{
							header = headerCreator(r);
							items.Add(id, header);
						}

						var detail = detailCreator(r, header);
						// Left Join support
						if (detail != null)
						{
							attach(header, detail);
						}
					}
				}
			}

			var result = new List<THeader>(items.Count);

			result.AddRange(items.Values);

			return result;
		}
	}";

		}
	}
}