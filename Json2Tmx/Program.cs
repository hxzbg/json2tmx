using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Json2Tmx
{
	class Program
	{
		static bool CheckExtition(string path)
		{
			int extindex = path.LastIndexOf('.');
			if(extindex <= 0)
			{
				return false;
			}
			if(String.CompareOrdinal(path, extindex, ".json", 0, 5) == 0)
			{
				return true;
			}
			if(String.CompareOrdinal(path, extindex, ".tsx", 0, 4) == 0)
			{
				return true;
			}
			if(String.CompareOrdinal(path, extindex, ".tmx", 0, 4) == 0)
			{
				return true;
			}
			return false;
		}

		static string CalcHash(string path)
		{
			if(File.Exists(path))
			{
				using (var md5 = System.Security.Cryptography.MD5.Create())
				{
					using (var stream = File.OpenRead(path))
					{
						byte[] hash = md5.ComputeHash(stream);
						return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
					}
				}
			}
			return null;			
		}

		/*
		返回码 0:文件不存在 1:解析成功 2:非json文件 3:解析失败
		*/
		static int ParseTilesetJson(string path)
		{
			string text = File.ReadAllText(path).Trim();
			JObject obj = JObject.Parse(text);
			if(obj == null)
			{
				return 2;
			}

			var tiles = obj["tiles"];
			if(tiles == null)
			{
				return 3;
			}

			string tilesetheader = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<tileset version=""{0}"" tiledversion=""{1}"" name=""{2}"" tilewidth=""{3}"" tileheight=""{4}"" tilecount=""{5}"" columns=""{6}"">
";
			tilesetheader = string.Format(tilesetheader, obj.Value<string>("version"), obj.Value<string>("tiledversion"), obj.Value<string>("name"), 
				obj.Value<uint>("tilewidth"), obj.Value<uint>("tileheight"), obj.Value<uint>("tilecount"), obj.Value<uint>("columns"));

			var grid = obj["grid"];
			if(grid != null)
			{
				tilesetheader += "\t<grid orientation=\"{0}\" width=\"{1}\" height=\"{2}\"/>\n";
				tilesetheader = string.Format(tilesetheader, grid.Value<string>("orientation"), grid.Value<uint>("width"), grid.Value<uint>("height"));
			}

			//填充tileset
			string tilestr = @"	<tile id=""{0}"">
		<image width=""{1}"" height=""{2}"" source=""{3}""/>
	</tile>
";
			StringBuilder builder = new StringBuilder(tilesetheader);
			foreach(var tile in tiles)
			{
				string source = tile.Value<string>("image");
				if(!string.IsNullOrEmpty(source))
				{
					source = source.Replace('\\', '/');
				}
				builder.AppendFormat(tilestr, tile.Value<uint>("id"), tile.Value<uint>("imagewidth"), tile.Value<uint>("imageheight"), source);
			}
			builder.Append("</tileset>");

			path = path.Substring(0, path.LastIndexOf('.')) + ".tsx";
			File.WriteAllText(path, builder.ToString());
			return 1;
		}

		/*
		返回码 0:文件不存在 1:解析成功 2:非json文件 3:解析失败
		*/
		static int ParseTileset(string path)
		{
			int extindex = path.LastIndexOf('.');
			if(extindex <= 0)
			{
				return 2;
			}

			if(String.CompareOrdinal(path, extindex, ".json", 0, 5) == 0)
			{
				return ParseTilesetJson(path);
			}
			return 2;
		}

		static string ParseTileset(string dir, JToken tileset)
		{
			StringBuilder builder = new StringBuilder();
			string header = "\t<tileset firstgid=\"{0}\" name=\"{1}\" tilewidth=\"{2}\" tileheight=\"{3}\" tilecount=\"{4}\" columns=\"{5}\">\n";
			header = string.Format(header, tileset.Value<uint>("firstgid"), tileset.Value<string>("name"), tileset.Value<uint>("tilewidth"), tileset.Value<uint>("tileheight"), tileset.Value<uint>("tilecount"), tileset.Value<uint>("columns"));
			builder.Append(header);

			//解析grid
			var grid = tileset["grid"];
			if(grid != null)
			{
				string gridstr = "\t\t<grid orientation=\"{0}\" width=\"{1}\" height=\"{2}\"/>\n";
				gridstr = string.Format(gridstr, grid.Value<string>("orientation"), grid.Value<uint>("width"), grid.Value<uint>("height"));
				builder.Append(gridstr);
			}

			//解析tiles
			var tiles = tileset["tiles"];
			if(tiles != null)
			{
				string tilestr = @"		<tile id=""{0}"">
			<image source=""{1}""/>
		</tile>
";
				foreach(var tile in tiles)
				{
					string source = tile.Value<string>("image");
					if(source != null)
					{
						source = source.Replace(dir, "").Replace('\\', '/');
						builder.Append(string.Format(tilestr, tile.Value<uint>("id"), source));
					}
				}
			}
			builder.Append("\t</tileset>\n");
			return builder.ToString();
		}

		static string Json2Tmx(string path)
		{
			int extindex = path.LastIndexOf('.');
			if(extindex <= 0)
			{
				return null;
			}

			if(String.CompareOrdinal(path, extindex, ".json", 0, 5) != 0)
			{
				return null;
			}

			string text = File.ReadAllText(path);
			JObject obj = JObject.Parse(text);
			var layers = obj["layers"];
			if (layers == null)
			{
				return null;
			}

			string dir = Path.GetDirectoryName(path).Replace('\\', '/') + "/";

			//解析layers
			StringBuilder builder = new StringBuilder();
			string header = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<map version=""1.10"" tiledversion=""1.10.1"" orientation=""{0}"" renderorder=""{1}"" width=""{2}"" height=""{3}"" tilewidth=""{4}"" tileheight=""{5}"" infinite=""{6}"" nextlayerid=""{7}"" nextobjectid=""{8}"">
";
			uint nextlayerid = obj.Value<uint>("nextlayerid");
			if(nextlayerid == 0)
			{
				nextlayerid = 1;
			}

			uint nextobjectid = obj.Value<uint>("nextobjectid");
			if(nextobjectid == 0)
			{
				nextobjectid = 1;
			}
			header = string.Format(header, obj.Value<string>("orientation"), obj.Value<string>("renderorder"), obj.Value<uint>("width"), 
				obj.Value<uint>("height"), obj.Value<uint>("tilewidth"), obj.Value<uint>("tileheight"), obj.Value<uint>("infinite"), nextlayerid, nextobjectid);
			builder.Append(header);

			//解析tilesets
			var tilesets = obj["tilesets"];
			if(tilesets != null)
			{
				foreach(var tiles in tilesets)
				{
					string source = tiles.Value<string>("source");
					if(source != null)
					{
						source = source.Replace(dir, "").Replace('\\', '/');
						source = source.Substring(0, source.LastIndexOf('.')) + ".tsx";
						string tileset = "\t<tileset firstgid=\"{0}\" source=\"{1}\"/>\n";
						tileset = string.Format(tileset, tiles.Value<uint>("firstgid"), source);
						builder.Append(tileset);
					}
					else
					{
						builder.Append(ParseTileset(dir, tiles));
					}
				}
			}

			string layer_str = @"	<layer id=""{0}"" name=""{1}"" width=""{2}"" height=""{3}"" visible=""{4}"">
		<data encoding=""{5}"" compression=""{6}"">
			{7}
		</data>
	</layer>
";
			string objectgroup_str = "\t<objectgroup id=\"{0}\" name=\"{1}\" visible=\"{2}\">\n";
			string object_str = "\t\t<object id=\"{0}\" gid=\"{1}\" x=\"{2}\" y=\"{3}\" width=\"{4}\" height=\"{5}\"/>\n";
			foreach (var layer in layers)
			{
				string type = layer.Value<string>("type");
				if(type == "tilelayer")
				{
					var data_json = layer["data"];
					if(data_json == null)
					{
						continue;
					}

					string data;
					string compression = layer.Value<string>("compression");
					string encoding = layer.Value<string>("encoding");
					int height = layer.Value<int>("height");
					int id = layer.Value<int>("id");
					string name = layer.Value<string>("name");
					bool visible = layer.Value<bool>("visible");
					int width = layer.Value<int>("width");
					
					if (data_json.GetType() == typeof(JArray))
					{
						encoding = "csv";
						StringBuilder data_builder = new StringBuilder();
						foreach(var item in data_json)
						{
							data_builder.AppendFormat("{0},", item);
						}
						if(data_builder.Length > 0)
						{
							data_builder.Remove(data_builder.Length - 1, 1);
						}
						data = data_builder.ToString();
					}
					else
					{
						data = data_json.Value<string>();
					}

					builder.AppendFormat(layer_str, id, name, width, height, visible ? 1 : 0, encoding, compression, data);
				}
				else if(type == "objectgroup")
				{
					var objects_data = layer["objects"];
					if(objects_data == null)
					{
						continue;
					}
					builder.AppendFormat(objectgroup_str, layer.Value<uint>("id"), layer.Value<string>("name"), layer.Value<bool>("visible") ? 1 : 0);
					foreach(var obj_data in objects_data)
					{
						builder.AppendFormat(object_str, obj_data.Value<uint>("id"), obj_data.Value<ulong>("gid"), obj_data.Value<float>("x"), obj_data.Value<float>("y"), obj_data.Value<int>("width"), obj_data.Value<int>("height"));
					}
					builder.Append("\t</objectgroup>\n");
				}
			}
			builder.Append("</map>");

			//写文件
			string tmxpath = path.Substring(0, extindex) + ".tmx";
			File.WriteAllText(tmxpath, builder.ToString());
			Console.WriteLine(string.Format("{0} -> {1}", path, tmxpath));
			return path;
		}

		static void Main(string[] args)
		{
			if (args.Length <= 0)
			{
				return;
			}

			//存储文件列表
			List<string> paths = new List<string>();
			for (int i = 0; i < args.Length; i++)
			{
				FileInfo info = new FileInfo(args[i]);
				if ((info.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
				{
					string[] files = Directory.GetFiles(args[i], "*.*", SearchOption.AllDirectories);
					foreach (var file in files)
					{
						if (CheckExtition(file))
						{
							paths.Add(file);
						}
					}
				}
				else if ((info.Attributes & FileAttributes.Archive) == FileAttributes.Archive)
				{
					if(CheckExtition(args[i]))
					{
						paths.Add(args[i]);
					}
				}
			}

			//解析图层依赖数据
			int path_count = paths.Count;
			for(int i = path_count - 1; i >= 0; i--)
			{
				string path = paths[i];
				int result = ParseTileset(path);
				if(result != 3)
				{
					paths.RemoveAt(i);
					if(result == 1)
					{
						Console.WriteLine(string.Format("解析 {0} 成功 {1} / {2}", path, path_count - i, path_count));
					}
				}
			}

			//解析tmx
			for (int i = 0; i < paths.Count; i++)
			{
				Json2Tmx(paths[i]);
			}
			Console.WriteLine("转换完成，任意键退出");
			Console.ReadKey();
		}
	}
}
