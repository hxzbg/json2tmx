using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Xml.Linq;
using System.Configuration;

namespace Json2Tmx
{
	class Program
	{
		class ObjectInfo
		{
			public int gid;
			public int value;
			public string layer;
		}
		static Dictionary<int, ObjectInfo> Objects = new Dictionary<int, ObjectInfo>();

		static Dictionary<int, int> IDMap = new Dictionary<int, int>();
		static Dictionary<string, int> TileSets = new Dictionary<string, int>();
		static void BuildIdMap()
		{
			IDMap.Clear();
			string text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "idmap.json")).Trim();
			JObject obj = JObject.Parse(text);

			JObject tilesets = obj.GetValue("tilesets") as JObject;
			foreach (var item in tilesets)
			{
				string key = item.Key.Trim();
				int value = int.Parse(item.Value.ToString());
				TileSets[key] = value;
			}

			JObject map = obj.GetValue("map") as JObject;
			foreach (var item in map)
			{
				string key = item.Key.Trim();
				JObject idset = item.Value as JObject;
				string tileset = idset["tileset"].ToString().Trim();
				IDMap[int.Parse(key)] = TileSets[tileset] + int.Parse(idset["id"].ToString());
			}
		}

		static bool CheckExtition(string path)
		{
			int extindex = path.LastIndexOf('.');
			if (extindex <= 0)
			{
				return false;
			}
			if (String.CompareOrdinal(path, extindex, ".json", 0, 5) == 0)
			{
				return true;
			}
			return false;
		}

		static string buildObjects(int width, ref int objectID)
		{
			string object_format = @"
		{0}
			""gid"":{2},
			""height"":32,
			""id"":{3},
			""name"":"""",
			""properties"":[
				{0}
					""name"":""id"",
					""type"":""int"",
					""value"":{6}
				{1}],
			""rotation"":0,
			""type"":"""",
			""visible"":true,
			""width"":32,
			""x"":{4},
			""y"":{5}
		{1},";

			if(Objects.Count <= 0)
			{
				return null;
			}

			var objects = new StringBuilder();
			var iter = Objects.GetEnumerator();
			while (iter.MoveNext())
			{
				int posid = iter.Current.Key;
				var info = iter.Current.Value;
				int x = posid >> 16;
				int y = posid & 0xFFFF;
				var item = string.Format(object_format, "{", "}", info.gid, objectID++, x * 32, y * 32, info.value);
				objects.Append(item);
			}
			objects.Remove(objects.Length - 1, 1);

			string layer_format = @"	{0}
		""draworder"":""topdown"",
		""name"":""objects"",
		""objects"":[{2}],
		""opacity"":1,
		""type"":""objectgroup"",
		""visible"":true,
		""x"":0,
		""y"":0
	{1}";
			return string.Format(layer_format, "{", "}", objects.ToString());
		}

		static string parseLayer(JObject mota_json, ref int layerid, string layername)
		{
			var map = mota_json[layername] as JArray;
			if(map == null)
			{
				return null;
			}

			int height = map.Count;
			int width = map[0].Count();

			var space = "\n\t\t";
			bool validate = false;
			var data = new StringBuilder("[");
			for (int i = 0; i < height; i++)
			{
				var items = map[i];
				for(int j = 0; j < width; j ++)
				{
					int value = 0;
					var item = items[j].Value<int>();
					if (item != 0)
					{
						if(IDMap.TryGetValue(item, out value))
						{
							int posid = j << 16 | i;
							if(Objects.TryGetValue(posid, out ObjectInfo info) && info.layer == layername)
							{
								info.gid = value;
								value = 0;
							}
							else
							{
								validate = true;
							}
						}
						else
						{
							value = 0;
							Console.WriteLine(string.Format("transform {0} failed, set to {1}", item, value));
						}
					}
					data.AppendFormat("{0},", value);
				}
				data.Append(space);
			}
			int removelen = space.Length + 1;
			data.Remove(data.Length - removelen, removelen);
			data.Append(']');

			if(!validate)
			{
				return null;
			}

			string layer_format = @"	{0}
		""data"":{2},
		""x"":0,
		""y"":0,
		""id"":{3},
		""name"":""{4}"",
		""width"":{5},
		""height"":{6},
		""opacity"":1,
		""type"":""tilelayer"",
		""visible"":true
	{1}";
			return string.Format(layer_format, "{", "}", data, layerid++, layername, width, height);
		}

		static string mota2Tmx(string path)
		{
			int extindex = path.LastIndexOf('.');
			if (extindex <= 0)
			{
				return null;
			}

			if (String.CompareOrdinal(path, extindex, ".json", 0, 5) != 0)
			{
				return null;
			}
			JObject mota_json = JObject.Parse(File.ReadAllText(path));

			string tiled_format = @"{0}
""compressionlevel"":-1,
 ""height"":{3},
 ""infinite"":false,
 ""layers"":[
{4}],
 ""nextlayerid"":{5},
 ""nextobjectid"":1,
 ""orientation"":""orthogonal"",
 ""renderorder"":""right-down"",
 ""tiledversion"":""1.10.2"",
 ""tileheight"":32,
 ""tilesets"":[{6}],
 ""tilewidth"":32,
 ""type"":""map"",
 ""version"":""1.10"",
 ""width"":{2}
{1}";

			//解析map、bgmap
			var width = mota_json["width"].Value<int>();
			var height = mota_json["height"].Value<int>();
			var tileProp = mota_json["tileProp"];

			Objects.Clear();
			if (tileProp != null)
			{
				foreach ( JProperty propItem in tileProp )
				{
					string[] pos = propItem.Name.Split(',');
					int x = int.Parse(pos[0].Trim());
					int y = int.Parse(pos[1].Trim());
					Objects[x << 16 | y] = new ObjectInfo
					{
						gid = -1,
						layer = "map",
						value = int.Parse(propItem.Value.ToString())
					};
				}
			}

			int nextlayerid = 1;
			var layers = new StringBuilder();
			string[] layerNames = new string[] { "bgmap", "map" };
            for(int i = 0; i < layerNames.Length; i ++)
			{
				var layer = parseLayer(mota_json, ref nextlayerid, layerNames[i]);
				if(layer != null)
				{
					layers.Append(layer);
					layers.Append(",\n");
				}
			}

			int nextObjectID = 1;
			{
				var layer = buildObjects(width, ref nextObjectID);
				if (layer != null)
				{
					layers.Append(layer);
					layers.Append(",\n");
				}
			}
			
			if(layers.Length > 2)
			{
				layers.Remove(layers.Length - 2, 2);
			}

			string tileset_format = @"
	{0}
		""firstgid"":{1},
		""source"":""{2}""
	{3},";

			//写tilesets信息
			var tilesets = new StringBuilder();
			var iter = TileSets.GetEnumerator();
			while(iter.MoveNext())
			{
				tilesets.Append(string.Format(tileset_format, '{', iter.Current.Value, iter.Current.Key, '}'));
			}
			if(TileSets.Count > 0)
			{
				tilesets.Remove(tilesets.Length - 1, 1);
			}

			//写文件
			string content = string.Format(tiled_format, "{", "}", width, height, layers, nextlayerid, tilesets);
			string tmxpath = path.Substring(0, extindex) + ".json";
			File.WriteAllText(tmxpath, content);
			Console.WriteLine(string.Format("{0} -> {1}", path, tmxpath));
			return path;
		}

		static void Main(string[] args)
		{
			BuildIdMap();
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
					if (CheckExtition(args[i]))
					{
						paths.Add(args[i]);
					}
				}
			}

			//解析tmx
			for (int i = 0; i < paths.Count; i++)
			{
				mota2Tmx(paths[i]);
			}
			Console.WriteLine("转换完成，任意键退出");
			Console.ReadKey();
		}
	}
}
