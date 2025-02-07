using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;

namespace SqlServerWorkspace
{
	public class Settings
	{
		public Rectangle WindowPosition { get; set; }
		public string ExternalExplorerSearchDirectory { get; set; } = string.Empty;
		public string ExternalExplorerSearchFilePattern { get; set; } = string.Empty;
		public string ExternalExplorerSearchProcedurePattern { get; set; } = string.Empty;
	}

	public static class ResourceManager
	{
		/* Connection */
		static readonly string connectionFileName = "connection.json";
		static readonly string settingsFileName = "settings.json";
		public static List<SqlManager> Connections = [];
		public static IEnumerable<IEnumerable<TreeNode>> ConnectionsNodes => Connections.Select(c => c.Nodes);
		public static Settings Settings = default!;
		private static Random random = new ();
		private static readonly string Code = "1234567890qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM";

		/* Icon Resource */
		public static readonly string ServerIcon = "M6 3h4v1H6V3zm0 6h4v1H6V9zm0 2h4v1H6v-1zm9.14 5H.86l1.25-5H4V2a.95.95 0 0 1 .078-.383c.052-.12.123-.226.211-.32a.922.922 0 0 1 .32-.219A1.01 1.01 0 0 1 5 1h6a.95.95 0 0 1 .383.078c.12.052.226.123.32.211a.922.922 0 0 1 .219.32c.052.125.078.256.078.391v9h1.89l1.25 5zM5 13h6V2H5v11zm8.86 2l-.75-3H12v2H4v-2H2.89l-.75 3h11.72z";
		public static readonly string ServerIconColor = "#07FC38";
		public static readonly string DatabaseIcon = "M13 3.5C13 2.119 10.761 1 8 1S3 2.119 3 3.5c0 .04.02.077.024.117H3v8.872l.056.357C3.336 14.056 5.429 15 8 15c2.571 0 4.664-.944 4.944-2.154l.056-.357V3.617h-.024c.004-.04.024-.077.024-.117zM8 2.032c2.442 0 4 .964 4 1.468s-1.558 1.468-4 1.468S4 4 4 3.5s1.558-1.468 4-1.468zm4 10.458l-.03.131C11.855 13.116 10.431 14 8 14s-3.855-.884-3.97-1.379L4 12.49v-7.5A7.414 7.414 0 0 0 8 6a7.414 7.414 0 0 0 4-1.014v7.504z";
		public static readonly string DatabaseIconColor = "#FFFF00";
		public static readonly string TableIcon = "M13.5 2h-12l-.5.5v11l.5.5h12l.5-.5v-11l-.5-.5zM2 3h11v1H2V3zm7 4H6V5h3v2zm0 1v2H6V8h3zM2 5h3v2H2V5zm0 3h3v2H2V8zm0 5v-2h3v2H2zm4 0v-2h3v2H6zm7 0h-3v-2h3v2zm0-3h-3V8h3v2zm-3-3V5h3v2h-3z";
		public static readonly string TableIconColor = "#00A2E8";
		public static readonly string ViewIcon = "M14 5H2V3h12v2zm0 4H2V7h12v2zM2 13h12v-2H2v2z";
		public static readonly string ViewIconColor = "#FFDF00";
		public static readonly string FunctionIcon = "M13.51 4l-5-3h-1l-5 3-.49.86v6l.49.85 5 3h1l5-3 .49-.85v-6L13.51 4zm-6 9.56l-4.5-2.7V5.7l4.5 2.45v5.41zM3.27 4.7l4.74-2.84 4.74 2.84-4.74 2.59L3.27 4.7zm9.74 6.16l-4.5 2.7V8.15l4.5-2.45v5.16z";
		public static readonly string FunctionIconColor = "#CC96F8";
		public static readonly string ProcedureIcon = "M9.1 4.4L8.6 2H7.4l-.5 2.4-.7.3-2-1.3-.9.8 1.3 2-.2.7-2.4.5v1.2l2.4.5.3.8-1.3 2 .8.8 2-1.3.8.3.4 2.3h1.2l.5-2.4.8-.3 2 1.3.8-.8-1.3-2 .3-.8 2.3-.4V7.4l-2.4-.5-.3-.8 1.3-2-.8-.8-2 1.3-.7-.2zM9.4 1l.5 2.4L12 2.1l2 2-1.4 2.1 2.4.4v2.8l-2.4.5L14 12l-2 2-2.1-1.4-.5 2.4H6.6l-.5-2.4L4 13.9l-2-2 1.4-2.1L1 9.4V6.6l2.4-.5L2.1 4l2-2 2.1 1.4.4-2.4h2.8zm.6 7c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zM8 9c.6 0 1-.4 1-1s-.4-1-1-1-1 .4-1 1 .4 1 1 1z";
		public static readonly string ProcedureIconColor = "#FEFEFE";

		/* Option */
		public static JsonSerializerOptions options = new() { WriteIndented = true };

		public static void Init()
		{
			LoadSettings();
			LoadConnectionInfo();
		}

		public static SqlManager? GetSqlManager(this TreeView treeView, TreeNode treeNode)
		{
			var treeViewItem = TreeViewManager.FindTreeViewItem(treeView, treeNode.Path);
			if (treeViewItem == null)
			{
				return null;
			}

			return GetSqlManager(treeViewItem);
		}

		public static SqlManager? GetSqlManager(TreeViewItem treeViewItem)
		{
			var topTreeViewItem = TreeViewManager.FindTopLevelNode(treeViewItem);
			if (topTreeViewItem == null)
			{
				return null;
			}

			var id = topTreeViewItem.Tag.ToString();
			if (id == null)
			{
				return null;
			}

			return GetSqlManager(id);
		}

		public static SqlManager? GetSqlManager(string id)
		{
			return Connections.Find(x => x.Id.Equals(id));
		}

		public static void LoadSettings()
		{
			if (!File.Exists(settingsFileName))
			{
				File.Create(settingsFileName);
				Settings = new Settings();
				return;
			}

			var settingsText = File.ReadAllText(settingsFileName);

			if (string.IsNullOrEmpty(settingsText))
			{
				Settings = new Settings();
				return;
			}

			Settings = JsonSerializer.Deserialize<Settings>(settingsText) ?? default!;
		}

		public static void LoadConnectionInfo()
		{
			if (!File.Exists(connectionFileName))
			{
				File.Create(connectionFileName);
				return;
			}

			var connectionInfoText = File.ReadAllText(connectionFileName);

			if (string.IsNullOrEmpty(connectionInfoText))
			{
				return;
			}

			Connections = (JsonSerializer.Deserialize<IEnumerable<SqlManager>>(connectionInfoText) ?? default!).ToList();

			foreach (var connection in Connections)
			{
				var server = connection.Server;
				var database = connection.Database;
				var user = connection.User;
				var password = connection.Password;
				var id = $"{server}_{random.Next(Code, 4)}";
				connection.Id = id;
				//TreeNode databaseNode = default!;
				switch (connection.AuthenticationType)
				{
					case AuthenticationType.WindowsAuthentication:
						{
							var databaseNode = new TreeNode(database, TreeNodeType.DatabaseNode, database);

							var tableNode = new TreeNode("Table", TreeNodeType.TableTitleNode, databaseNode.Path.CombinePath("Table"));
							var viewNode = new TreeNode("View", TreeNodeType.ViewTitleNode, databaseNode.Path.CombinePath("View"));
							var functionNode = new TreeNode("Function", TreeNodeType.FunctionTitleNode, databaseNode.Path.CombinePath("Function"));
							var procedureNode = new TreeNode("Procedure", TreeNodeType.ProcedureTitleNode, databaseNode.Path.CombinePath("Procedure"));
							var tableNames = connection.SelectTableNames();
							foreach (var tableName in tableNames)
							{
								tableNode.Children.Add(new TreeNode(tableName, TreeNodeType.TableNode, tableNode.Path.CombinePath(tableName)));
							}
							databaseNode.Children.Add(tableNode);

							var viewNames = connection.SelectViewNames();
							foreach (var viewName in viewNames)
							{
								viewNode.Children.Add(new TreeNode(viewName, TreeNodeType.ViewNode, tableNode.Path.CombinePath(viewName)));
							}
							databaseNode.Children.Add(viewNode);

							var functionNames = connection.SelectFunctionNames();
							foreach (var functionName in functionNames)
							{
								functionNode.Children.Add(new TreeNode(functionName, TreeNodeType.FunctionNode, tableNode.Path.CombinePath(functionName), FunctionIcon));
							}
							databaseNode.Children.Add(functionNode);

							var procedureNames = connection.SelectProcedureNames();
							foreach (var procedureName in procedureNames)
							{
								procedureNode.Children.Add(new TreeNode(procedureName, TreeNodeType.ProcedureNode, tableNode.Path.CombinePath(procedureName)));
							}
							databaseNode.Children.Add(procedureNode);

							connection.Nodes.Add(databaseNode);
						}
						break;

					case AuthenticationType.SqlServerAuthentication:
						{
							var serverNode = new TreeNode(id, TreeNodeType.ServerNode, id, ServerIcon, ServerIconColor)
							{
								IsExpanded = !string.IsNullOrEmpty(database)
							};

							var databaseNames = string.IsNullOrEmpty(database) ? connection.SelectDatabaseNames() : database.Split(',', StringSplitOptions.RemoveEmptyEntries);
							foreach (var databaseName in databaseNames)
							{
								serverNode.Children.Add(new TreeNode(databaseName, TreeNodeType.DatabaseNode, serverNode.Path.CombinePath(databaseName), DatabaseIcon, DatabaseIconColor));
							}

							connection.Nodes.Add(serverNode);
						}
						break;

					default:
						break;
				}
			}
		}

		public static void SaveSettings()
		{
			File.WriteAllText(settingsFileName, JsonSerializer.Serialize(Settings, options));
		}

		public static void SaveConnectionInfo()
		{
			File.WriteAllText(connectionFileName, JsonSerializer.Serialize<IEnumerable<SqlManager>>(Connections, options));
		}

		public static string Next(this Random random, string str)
		{
			return str[random.Next(str.Length)].ToString();
		}

		public static string Next(this Random random, string str, int count)
		{
			string result = string.Empty;
			for (int i = 0; i < count; i++)
			{
				result += random.Next(str);
			}
			return result;
		}
	}
}
