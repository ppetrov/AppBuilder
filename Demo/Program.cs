﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;
using AppBuilder;
using AppBuilder.Db.DDL;

namespace Demo
{
	public class Program
	{
		static void Main(string[] args)
		{
			//using (var cn = new SQLiteConnection(@"Data Source=C:\Users\bg900343\Desktop\store.sqlite"))
			//{
			//	cn.Open();
			//	QueryHelper.Connection = cn;
			//	QueryHelper.ParameterCreator = (name, v) => new SQLiteParameter(name, v);

			//	var bds = QueryHelper.Get(@"select id, name from brands", r => r.GetInt64(0) + " : " + r.GetString(1));
			//	foreach (var bd in bds)
			//	{
			//		Console.WriteLine(bd);
			//	}



			//	QueryHelper.ExecuteQuery(@"insert into brands(name) values('A')");
			//	var value = QueryHelper.ExecuteScalar(@"SELECT LAST_INSERT_ROWID()");
			//	Console.WriteLine(value);

			//}
			//return;


			var articleTypes = DbTable.ReadOnly(@"ArticleTypes", new[]
			                                                     {
				                                                     DbColumn.PrimaryKey(),
				                                                     DbColumn.String(@"Name"),
			                                                     });
			var brands = DbTable.ReadOnly(@"Brands", new[]
			                                         {
				                                         DbColumn.PrimaryKey(),
				                                         DbColumn.String(@"Name"),
			                                         });

			var flavours = DbTable.ReadOnly(@"Flavours", new[]
			                                             {
				                                             DbColumn.PrimaryKey(),
				                                             DbColumn.String(@"Name"),
			                                             });

			var articles = DbTable.ReadOnly(@"Articles", new[]
			                                             {
				                                             DbColumn.PrimaryKey(),
				                                             DbColumn.String(@"Name"),
				                                             DbColumn.ForeignKey(articleTypes),
				                                             DbColumn.ForeignKey(brands),
				                                             DbColumn.ForeignKey(flavours),
				                                             DbColumn.Decimal(@"Price"),
			                                             });

			var deliveryLocation = DbTable.ReadOnly(@"DeliveryLocations", new[]
			                                                              {
				                                                              DbColumn.PrimaryKey(),
				                                                              DbColumn.String(@"Name"),
			                                                              });
			var outlets = DbTable.ReadOnly(@"Outlets", new[]
			                                           {
				                                           DbColumn.PrimaryKey(),
				                                           DbColumn.String(@"Name"),
				                                           DbColumn.String(@"Address"),
				                                           DbColumn.String(@"City"),
				                                           DbColumn.String(@"Street"),
				                                           DbColumn.ForeignKey(deliveryLocation),
			                                           });

			var users = DbTable.ReadOnly(@"Users", new[]
			                                       {
				                                       DbColumn.PrimaryKey(),
				                                       DbColumn.String(@"LoginName"),
				                                       DbColumn.String(@"FullName"),
			                                       });

			var visits = DbTable.Normal(@"Visits", new[]
			                                       {
													   DbColumn.PrimaryKey(),
													   DbColumn.DateTime(@"Date"),
													   DbColumn.ForeignKey(outlets),
													   DbColumn.ForeignKey(users),
			                                       });

			var activityTypes = DbTable.ReadOnly(@"ActivityTypes", new[]
			                                                        {
				                                                        DbColumn.PrimaryKey(),
				                                                        DbColumn.String(@"Name"),
				                                                        DbColumn.String(@"Code"),
			                                                        });

			var activities = DbTable.Normal(@"Activities", new[]
			                                               {
															   DbColumn.PrimaryKey(),
															   DbColumn.ForeignKey(activityTypes),
															   DbColumn.ForeignKey(visits),
															   DbColumn.DateTime(@"ValidFrom"),
															   DbColumn.DateTime(@"ValidTo"),
			                                               }, @"Activity");

			var calendarDays = DbTable.Normal(@"CalendarDays", new[]
			                                                        {
				                                                        DbColumn.PrimaryKey(),
				                                                        DbColumn.DateTime(@"VisitDate"),
				                                                        DbColumn.Integer(@"Status"),
				                                                        DbColumn.ForeignKey(users),
			                                                        });

			var logs = DbTable.Normal(@"LogMessages", new[]
			                                                        {
				                                                        DbColumn.PrimaryKey(),
				                                                        DbColumn.DateTime(@"Time"),
				                                                        DbColumn.String(@"Type"),
				                                                        DbColumn.String(@"Message"),
			                                                        });

			var ifsa = new[]
			           {
						   articleTypes,
						   brands,
						   flavours,
						   articles,
						   deliveryLocation,
						   outlets,
						   users,
						   visits,
						   activityTypes,
						   activities,
						   calendarDays,
						   logs
			           };

			DumpDDL(ifsa);
			DumpClasses(ifsa);
			DumpAdapters(ifsa);



			//var brands = DbTable.ReadOnly(@"Brands", new[]
			//										  {
			//											  DbColumn.PrimaryKey(),
			//											  DbColumn.String(@"Name"),
			//										  });
			//var flavours = DbTable.ReadOnly(@"Flavours", new[]
			//									{
			//										DbColumn.PrimaryKey(),
			//										DbColumn.String(@"Name"),
			//									});
			//var dbArticles = DbTable.ReadOnly(@"Articles", new[]
			//											   {
			//												   DbColumn.PrimaryKey(),
			//												   DbColumn.String(@"Name"),
			//												   DbColumn.ForeignKey(brands),
			//												   DbColumn.ForeignKey(flavours),
			//											   });
			//var dbOrderHeader = DbTable.Normal(@"OrderHeaders", new[]
			//											  {
			//												  DbColumn.PrimaryKey(),
			//												  DbColumn.String(@"Name")
			//											  });
			//var dbOrderDetails = DbTable.Normal(@"OrderDetails", new[]
			//											  {
			//												  DbColumn.PrimaryKey(),
			//												  DbColumn.ForeignKey(dbOrderHeader),
			//												  DbColumn.ForeignKey(dbArticles),
			//												  DbColumn.Integer(@"Quantity"),
			//											  });

			//var dbTables = new[]
			//			   {
			//				   dbArticles,							   
			//				   dbOrderDetails,
			//				   dbOrderHeader,
			//				   brands,
			//				   flavours,
			//			   };

			//DumpTables(dbTables);
		}

		private static void DumpDDL(DbTable[] tables)
		{
			var script = DbSchemaParser.GenerateScript(new DbSchema(@"iFSA", tables));
			File.WriteAllText(@"C:\temp\schema.sql", script);

			var schema = DbSchemaParser.Parse(script);
			for (int index = 0; index < schema.Tables.Length; index++)
			{
				var a = schema.Tables[index];
				var b = tables[index];
				Console.WriteLine(IsEqual(a, b));
			}
		}

		private static void DumpClasses(DbTable[] tables)
		{
			var buffer = new StringBuilder();
			foreach (var table in tables)
			{
				var code = ObjectGenerator.GenerateCode(DbTableConverter.ToClrClass(table, tables), table.IsReadOnly);
				buffer.AppendLine(code);
			}
			File.WriteAllText(@"C:\temp\obj.cs", buffer.ToString());
		}

		private static void DumpAdapters(DbTable[] tables)
		{
			var schema = new DbSchema(string.Empty, tables);
			var buffer = new StringBuilder();
			foreach (var table in tables)
			{
				//if ( table.Name != @"Brands")
				//if (table.Name != @"Articles")
				if (table.IsReadOnly)
				//if (table.Name != @"Activities")
				//if (table.Name != @"CalendarDays")
				//if (false)
				{
					continue;
				}
				var code = AdapterGenerator.GenerateCode(DbTableConverter.ToClrClass(table, tables), table, schema) + Environment.NewLine;
				buffer.AppendLine(code);
			}
			File.WriteAllText(@"C:\temp\ada.cs", buffer.ToString());
		}



		//private static void Display(DbQuery query)
		//{
		//	Console.WriteLine(query.Statement);
		//	Console.WriteLine(query.Parameters.Length);
		//	foreach (var p in query.Parameters)
		//	{
		//		Console.WriteLine(p.Name);
		//	}
		//	Console.WriteLine();
		//	Console.WriteLine();
		//}

		public static bool IsEqual(DbTable a, DbTable b)
		{
			if (a == null) throw new ArgumentNullException("a");
			if (b == null) throw new ArgumentNullException("b");

			if (a.Name.Equals(b.Name))
			{
				var colsA = a.Columns;
				var colsB = b.Columns;
				if (colsA.Length == colsB.Length)
				{
					for (var i = 0; i < colsA.Length; i++)
					{
						var ca = colsA[i];
						var cb = colsB[i];
						if (!IsEqual(ca, cb))
						{
							return false;
						}
					}
					return true;
				}
			}

			return false;
		}

		private static bool IsEqual(DbColumn a, DbColumn b)
		{
			var x = GetColumnKey(a);
			var y = GetColumnKey(b);
			return x.Equals(y);
		}

		private static string GetColumnKey(DbColumn a)
		{
			return string.Join(@"|", new[]
			                         {
				                         a.AllowNull.ToString(),
				                         a.Name,
				                         GetForeignKeyKey(a),
				                         a.IsPrimaryKey.ToString(),
				                         GetTypeKey(a)
			                         });
		}

		private static string GetForeignKeyKey(DbColumn a)
		{
			var fk = a.DbForeignKey;
			if (fk != null)
			{
				return fk.Table + @"|" + fk.Column;
			}
			return string.Empty;
		}

		private static string GetTypeKey(DbColumn a)
		{
			return a.Type.Name;
		}
	}
}