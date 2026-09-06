#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerrariazhcn
{
	/// <summary>
	/// GregTech 自带的 "Too Many Items" 全局配方浏览器显示工作台类型时,
	/// 不查本地化, 而是把配方类型内部名 (如 alloy_smelter) 直接 Humanize 成
	/// "Alloy Smelter" (见 GregTechCEuTerraria.TerrariaCompat.UI.Widgets.StationIcon
	/// 与 RecipeRowRenderer)。这里在 PostSetupContent 之后, 把已翻译的
	/// RecipeTypeName 文案通过 StationIcon.RegisterDisplayName 注册进去,
	/// 让浏览器里的工作台类型显示中文。
	/// </summary>
	public static class StationNameLocalizer
	{
		private const string TargetMod = "GregTechCEuTerraria";
		private const string StationIconType = "GregTechCEuTerraria.TerrariaCompat.UI.Widgets.StationIcon";
		private const string RecipeTypesType = "GregTechCEuTerraria.Common.Recipe.GTRecipeTypes";
		private const string RecipeRegistryType = "GregTechCEuTerraria.TerrariaCompat.Recipes.RecipeRegistry";

		private static readonly List<string> Registered = new();

		public static void PostSetupContent()
		{
			if (!ModLoader.TryGetMod(TargetMod, out var gt) || gt.Code is null)
			{
				return;
			}
			var asm = gt.Code;

			var register = asm.GetType(StationIconType)
				?.GetMethod("RegisterDisplayName", BindingFlags.Public | BindingFlags.Static);
			if (register is null)
			{
				return;
			}

			var ids = CollectStationIds(asm);
			foreach (var id in ids)
			{
				if (string.IsNullOrEmpty(id) || Registered.Contains(id))
				{
					continue;
				}
				var key = $"Mods.{TargetMod}.RecipeTypeName.{id}";
				if (!Language.Exists(key))
				{
					continue;
				}
				var value = Language.GetTextValue(key);
				if (string.IsNullOrWhiteSpace(value) || value == key)
				{
					continue;
				}
				register.Invoke(null, new object[] { id, value });
				Registered.Add(id);
			}
		}

		public static void Unload()
		{
			if (Registered.Count == 0)
			{
				return;
			}
			if (ModLoader.TryGetMod(TargetMod, out var gt) && gt.Code is not null)
			{
				var field = gt.Code.GetType(StationIconType)
					?.GetField("_displayNames", BindingFlags.NonPublic | BindingFlags.Static);
				if (field?.GetValue(null) is IDictionary<string, string> dict)
				{
					foreach (var id in Registered)
					{
						dict.Remove(id);
					}
				}
			}
			Registered.Clear();
		}

		private static HashSet<string> CollectStationIds(Assembly asm)
		{
			var ids = new HashSet<string>(StringComparer.Ordinal);

			// 1) GTRecipeTypes 静态字段: 覆盖全部机器配方类型 (与配方是否加载无关)
			try
			{
				var types = asm.GetType(RecipeTypesType);
				var fields = types?.GetFields(BindingFlags.Public | BindingFlags.Static);
				PropertyInfo? registryName = null;
				if (fields != null)
				{
					foreach (var f in fields)
					{
						if (registryName is null)
						{
							if (f.FieldType.GetProperty("RegistryName") is not { } prop)
							{
								continue;
							}
							registryName = prop;
						}
						if (f.FieldType == registryName.DeclaringType
							&& f.GetValue(null) is { } type
							&& registryName.GetValue(type) is string id
							&& !string.IsNullOrEmpty(id))
						{
							ids.Add(id);
						}
					}
				}
			}
			catch (Exception e)
			{
				gtLog($"枚举 GTRecipeTypes 失败: {e.Message}");
			}

			// 2) RecipeRegistry.ByStation 的键: 兜底覆盖原生桥接等额外工作站
			try
			{
				var registry = asm.GetType(RecipeRegistryType);
				var byStation = registry?.GetProperty("ByStation")?.GetValue(null);
				var keys = byStation?.GetType().GetProperty("Keys")?.GetValue(byStation)
					as IEnumerable<string>;
				if (keys != null)
				{
					foreach (var id in keys)
					{
						ids.Add(id);
					}
				}
			}
			catch (Exception e)
			{
				gtLog($"枚举 RecipeRegistry.ByStation 失败: {e.Message}");
			}

			return ids;
		}

		private static void gtLog(string message)
		{
			ModLoader.TryGetMod(TargetMod, out var gt);
			(gt?.Logger ?? ModLoader.Mods[^1].Logger)?.Info($"[GregTechCEuTerrariazhcn] {message}");
		}
	}
}
