#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using MonoMod.RuntimeDetour;

namespace GregTechCEuTerrariazhcn
{
	/// <summary>
	/// 配方条件提示里有一部分是运行时拼进去的值, 不是 IL 字面量, TigerForce 的字面量替换
	/// 覆盖不到, 游戏里会中英混排:
	///   需要洁净室：cleanroom / sterile_cleanroom      (CleanroomCondition)
	///   需要生物群系：Jungle / Underground Desert       (BiomeCondition, 值来自矿物桶名或 BiomeProbe.Biome)
	///   需要生物群系标签：minecraft:is_cold             (BiomeTagCondition)
	///   需要环境危害：gtceu:carbon_monoxide_poisoning   (EnvironmentalHazardCondition)
	/// 洁净室机器的悬停提示里也有同样的值 (类型：cleanroom)。
	/// 这里给这些方法的返回值挂 MonoMod 运行时 detour, 返回前把动态值换成中文。
	///
	/// 注意: MonoMod 的 Hook(MethodBase, Delegate) 要求挂钩委托是静态方法且没有闭包
	/// 对象, 否则报 "Target method is static, but a target object was provided"; 所以
	/// 用泛型静态钩子方法 + Delegate.CreateDelegate, 而不是 Expression.Compile (后者
	/// 生成的是带闭包的实例委托)。
	/// </summary>
	public static class ConditionValueLocalizer
	{
		private const string ConditionNamespace = "GregTechCEuTerraria.Common.Recipe.Condition.";
		private const string CleanroomMachineType = "GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric.CleanroomMachine";

		private static readonly List<IDisposable> Hooks = new();
		private static readonly HashSet<string> Hooked = new(StringComparer.Ordinal);
		private static readonly Dictionary<Type, KeyValuePair<string, string>[]> Maps = new();
		private static Action<string> _log = _ => { };

		private static readonly KeyValuePair<string, string>[] CleanroomNames = Map(
			("sterile_cleanroom", "无菌洁净室"),
			("cleanroom", "洁净室"));

		private static readonly KeyValuePair<string, string>[] BiomeNames = Map(
			// 原版生物群系 id (JSON 条件用)
			("minecraft:jungle", "丛林"),
			("minecraft:desert", "沙漠"),
			("minecraft:nether", "地狱"),
			("minecraft:ocean", "海洋"),
			("minecraft:mushroom", "蘑菇地"),
			("minecraft:plains", "平原"),
			("minecraft:forest", "森林"),
			// 大型矿机 (BiomeWorldIOTables.Label 拆驼峰) 与钻井机 (BiomeProbe.Biome.ToString) 用的标签
			("Underground Desert", "地下沙漠"),
			("Underground Snow", "地下雪原"),
			("Underground Jungle", "地下丛林"),
			("Underground Corruption", "地下腐化之地"),
			("Underground Crimson", "地下猩红之地"),
			("Underground Hallow", "地下神圣之地"),
			("Underground Mushroom", "地下蘑菇地"),
			("Cavern Desert", "洞穴沙漠"),
			("Cavern Snow", "洞穴雪原"),
			("Cavern Jungle", "洞穴丛林"),
			("Cavern Corruption", "洞穴腐化之地"),
			("Cavern Crimson", "洞穴猩红之地"),
			("Cavern Hallow", "洞穴神圣之地"),
			("Cavern Mushroom", "洞穴蘑菇地"),
			("Gem Cave", "宝石洞"),
			("Lihzahrd Temple", "丛林蜥蜴神庙"),
			("Underground", "地下"),
			("Underworld", "地狱"),
			("Cavern", "洞穴"),
			("Forest", "森林"),
			("Desert", "沙漠"),
			("Snow", "雪原"),
			("Jungle", "丛林"),
			("Ocean", "海洋"),
			("Mushroom", "蘑菇地"),
			("Corruption", "腐化之地"),
			("Crimson", "猩红之地"),
			("Hallow", "神圣之地"),
			("Granite", "花岗岩"),
			("Marble", "大理石"),
			("Hive", "蜂巢"),
			("Dungeon", "地牢"));

		private static readonly KeyValuePair<string, string>[] BiomeTagNames = Map(
			("minecraft:is_cold", "寒冷"),
			("minecraft:is_hot", "炎热"),
			("minecraft:is_jungle", "丛林"));

		private static readonly KeyValuePair<string, string>[] HazardNames = Map(
			("gtceu:carbon_monoxide_poisoning", "一氧化碳中毒"));

		// BiomeCondition 的 "minecraft:snowy_*" 系列 id 都落到同一个雪原
		private static readonly Regex SnowyBiomeId = new(@"minecraft:snowy_\w+", RegexOptions.Compiled);

		private static readonly MethodInfo HookNoArgsMethod = typeof(ConditionValueLocalizer)
			.GetMethod(nameof(HookNoArgs), BindingFlags.NonPublic | BindingFlags.Static)!;

		private static readonly MethodInfo HookOneArgMethod = typeof(ConditionValueLocalizer)
			.GetMethod(nameof(HookOneArg), BindingFlags.NonPublic | BindingFlags.Static)!;

		private static readonly MethodInfo HookTooltipListMethod = typeof(ConditionValueLocalizer)
			.GetMethod(nameof(HookTooltipList), BindingFlags.NonPublic | BindingFlags.Static)!;

		public static void Load(Assembly targetAssembly, Action<string> log)
		{
			_log = log ?? (_ => { });

			HookReturnText(targetAssembly, "CleanroomCondition", "GetTooltips", CleanroomNames);
			HookReturnText(targetAssembly, "CleanroomCondition", "GetFailureMessage", CleanroomNames);
			HookReturnText(targetAssembly, "BiomeCondition", "GetTooltips", BiomeNames);
			HookReturnText(targetAssembly, "BiomeTagCondition", "GetTooltips", BiomeTagNames);
			HookReturnText(targetAssembly, "EnvironmentalHazardCondition", "GetTooltips", HazardNames);
			HookAppendTooltip(targetAssembly, CleanroomMachineType, "AppendTooltip", CleanroomNames);
		}

		public static void Unload()
		{
			foreach (var hook in Hooks)
			{
				try
				{
					hook.Dispose();
				}
				catch
				{
					// 卸载阶段目标程序集可能已经卸载, 忽略
				}
			}
			Hooks.Clear();
			Hooked.Clear();
			Maps.Clear();
			_log = _ => { };
		}

		/// <summary>给返回 string 的方法挂 detour, 返回前替换动态值。</summary>
		private static void HookReturnText(Assembly asm, string typeName, string methodName,
			KeyValuePair<string, string>[] map)
		{
			var key = $"{typeName}.{methodName}";
			if (Hooked.Contains(key))
			{
				return;
			}
			try
			{
				var type = asm.GetType(ConditionNamespace + typeName, throwOnError: false);
				var method = FindMethod(type, methodName);
				if (method is null || method.ReturnType != typeof(string))
				{
					_log($"[ConditionValueLocalizer] 未找到 {typeName}.{methodName}() : string, 跳过");
					return;
				}

				var hookInfo = BuildReturnTextHook(method);
				if (hookInfo is null)
				{
					_log($"[ConditionValueLocalizer] {typeName}.{methodName} 参数个数不支持, 跳过");
					return;
				}

				Maps[method.DeclaringType!] = map;
				var hook = Delegate.CreateDelegate(hookInfo.Value.DelegateType, hookInfo.Value.HookMethod);
				Hooks.Add(new Hook(method, hook));
				Hooked.Add(key);
			}
			catch (Exception e)
			{
				_log($"[ConditionValueLocalizer] 挂钩 {key} 失败: {e.GetBaseException().Message}");
			}
		}

		/// <summary>给 AppendTooltip(List&lt;string&gt;) 挂 detour, 把已加入的行就地替换。</summary>
		private static void HookAppendTooltip(Assembly asm, string typeName, string methodName,
			KeyValuePair<string, string>[] map)
		{
			var key = $"{typeName}.{methodName}";
			if (Hooked.Contains(key))
			{
				return;
			}
			try
			{
				var type = asm.GetType(typeName, throwOnError: false);
				var method = FindMethod(type, methodName);
				var ps = method?.GetParameters();
				if (method is null || method.ReturnType != typeof(void)
					|| ps is null || ps.Length != 1 || ps[0].ParameterType != typeof(List<string>))
				{
					_log($"[ConditionValueLocalizer] 未找到 {typeName}.{methodName}(List<string>), 跳过");
					return;
				}

				var instType = method.DeclaringType!;
				var origType = Expression.GetDelegateType(instType, typeof(List<string>), typeof(void));
				var hookType = Expression.GetDelegateType(origType, instType, typeof(List<string>), typeof(void));
				var hookMethod = HookTooltipListMethod.MakeGenericMethod(instType);

				Maps[instType] = map;
				Hooks.Add(new Hook(method, Delegate.CreateDelegate(hookType, hookMethod)));
				Hooked.Add(key);
			}
			catch (Exception e)
			{
				_log($"[ConditionValueLocalizer] 挂钩 {key} 失败: {e.GetBaseException().Message}");
			}
		}

		/// <summary>
		/// 按目标方法的实际签名拼出钩子委托类型: GetTooltips() 无参,
		/// GetFailureMessage(RecipeLogic) 一参, 用表达式树取对应的 Func 类型。
		/// </summary>
		private static (Type DelegateType, MethodInfo HookMethod)? BuildReturnTextHook(MethodInfo method)
		{
			var instType = method.DeclaringType!;
			var argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();

			switch (argTypes.Length)
			{
				case 0:
				{
					var origType = Expression.GetDelegateType(instType, typeof(string));
					var hookType = Expression.GetDelegateType(origType, instType, typeof(string));
					return (hookType, HookNoArgsMethod.MakeGenericMethod(instType));
				}
				case 1:
				{
					var origType = Expression.GetDelegateType(instType, argTypes[0], typeof(string));
					var hookType = Expression.GetDelegateType(origType, instType, argTypes[0], typeof(string));
					return (hookType, HookOneArgMethod.MakeGenericMethod(instType, argTypes[0]));
				}
				default:
					return null;
			}
		}

		/// <summary>static 且无闭包: MonoMod 的 delegate 钩子要求。</summary>
		private static string HookNoArgs<TInstance>(Func<TInstance, string> orig, TInstance self) =>
			Translate(GetMap(typeof(TInstance)), orig(self));

		private static string HookOneArg<TInstance, TArg>(Func<TInstance, TArg, string> orig, TInstance self, TArg arg) =>
			Translate(GetMap(typeof(TInstance)), orig(self, arg));

		private static void HookTooltipList<TInstance>(Action<TInstance, List<string>> orig, TInstance self,
			List<string> lines)
		{
			orig(self, lines);
			TranslateList(GetMap(typeof(TInstance)), lines);
		}

		private static KeyValuePair<string, string>[] GetMap(Type instanceType) =>
			Maps.TryGetValue(instanceType, out var map) ? map : Array.Empty<KeyValuePair<string, string>>();

		/// <summary>按名字取实例方法; 同名重载只可能有一个 (GetTooltips / GetFailureMessage / AppendTooltip)。</summary>
		private static MethodInfo? FindMethod(Type? type, string methodName) =>
			type?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m => m.Name == methodName && !m.IsAbstract);

		private static string Translate(KeyValuePair<string, string>[] map, string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}
			foreach (var pair in map)
			{
				if (text.Contains(pair.Key))
				{
					text = text.Replace(pair.Key, pair.Value);
				}
			}
			return SnowyBiomeId.Replace(text, "雪原");
		}

		private static void TranslateList(KeyValuePair<string, string>[] map, List<string> lines)
		{
			for (int i = 0; i < lines.Count; i++)
			{
				lines[i] = Translate(map, lines[i]);
			}
		}

		/// <summary>按值长度降序, 保证 "Underground Desert" 先于 "Desert" 命中。</summary>
		private static KeyValuePair<string, string>[] Map(params (string From, string To)[] pairs) =>
			pairs.OrderByDescending(p => p.From.Length)
				.Select(p => new KeyValuePair<string, string>(p.From, p.To))
				.ToArray();
	}
}
