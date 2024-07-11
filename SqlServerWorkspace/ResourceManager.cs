using SqlServerWorkspace.Data;
using SqlServerWorkspace.DataModels;
using SqlServerWorkspace.Enums;
using SqlServerWorkspace.Extensions;

using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SqlServerWorkspace
{
	public static class ResourceManager
	{
		static readonly string connectionFileName = "connection.json";
		public static List<SqlManager> Connections = [];
		public static BitmapImage FunctionIcon = default!;
		public static JsonSerializerOptions options = new() { WriteIndented = true };

		public static void Init()
		{
			FunctionIcon = new BitmapImage();
			FunctionIcon.BeginInit();
			FunctionIcon.UriSource = new Uri("pack://application:,,,/Resources/Icons/function.png");
			FunctionIcon.CacheOption = BitmapCacheOption.OnLoad;
			FunctionIcon.EndInit();

			LoadConnectionInfo();
		}

		public static SqlManager? GetSqlManager(this TreeView treeView, TreeNode treeNode)
		{
			var treeViewItem = TreeViewManager.FindTreeViewItem(treeView, treeNode.Path);
			if (treeViewItem == null)
			{
				return null;
			}

			var topTreeViewItem = TreeViewManager.FindTopLevelNode(treeViewItem);
			if (topTreeViewItem == null)
			{
				return null;
			}

			var server = topTreeViewItem.Tag.ToString();
			if (server == null)
			{
				return null;
			}

			return GetSqlManager(server);
		}

		public static SqlManager? GetSqlManager(string server)
		{
			return Connections.Find(x => x.Server.Equals(server));
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
				//TreeNode databaseNode = default!;
				switch (connection.AuthenticationType)
				{
					case AuthenticationType.WindowsAuthentication:
						var databaseNode = new TreeNode(database, TreeNodeType.DatabaseNode, database);

						var tableNode = new TreeNode("Table", TreeNodeType.TableTitleNode, databaseNode.Path.CombinePath("Table"));
						var viewNode = new TreeNode("View", TreeNodeType.ViewTitleNode, databaseNode.Path.CombinePath("View"));
						var functionNode = new TreeNode("Function", TreeNodeType.FunctionTitleNode, databaseNode.Path.CombinePath("Function"), FunctionIcon);
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
						break;

					case AuthenticationType.SqlServerAuthentication:
						var serverNode = new TreeNode(server, TreeNodeType.ServerNode, server);

						var databaseNames = connection.SelectDatabaseNames();
						foreach (var databaseName in databaseNames)
						{
							serverNode.Children.Add(new TreeNode(databaseName, TreeNodeType.DatabaseNode, serverNode.Path.CombinePath(databaseName)));
						}

						connection.Nodes.Add(serverNode);
						break;

					default:
						break;
				}
			}
		}

		public static void SaveConnectionInfo()
		{
			File.WriteAllText(connectionFileName, JsonSerializer.Serialize<IEnumerable<SqlManager>>(Connections, options));
		}
	}
}
