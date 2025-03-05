using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.SliderDelimiter
{
	[BepInPlugin(GUID, Name, Version)]
	public class SliderDelimiter : BaseUnityPlugin
	{
		private const string GUID = "org.krypto5863.com3d2.sliderdelimiter";
		private const string Name = "SliderDelimiter";
		private const string Version = "1.0";

		private static ConfigEntry<float> _sliderRangeMultiplier;

		public void Awake()
		{
			_sliderRangeMultiplier = Config.Bind("General", "Multiplier", 1.5f, new ConfigDescription("Multiplier for range increments.", new AcceptableValueRange<float>(1.5f, 3.0f)));

			Harmony.CreateAndPatchAll(typeof(Hooks));
		}

		private static class Hooks
		{
			[HarmonyPostfix]
			[HarmonyPatch(typeof(MaidProp), MethodType.Constructor)]
			[HarmonyPatch(typeof(MaidProp), nameof(MaidProp.Deserialize))]
			private static void DelimitSliders(ref MaidProp __instance) => DelimitSliders2(ref __instance);

			[HarmonyPostfix]
			[HarmonyPatch(typeof(Maid), nameof(Maid.CreateProp), typeof(int), typeof(int), typeof(int), typeof(MPN), typeof(int))]
			private static void DelimitSliders2(ref MaidProp __result)
			{
				if (__result.type == 3)
				{
					return;
				}

				var oldMax = __result.max;
				__result.max = (int)(_sliderRangeMultiplier.Value * __result.max);
				__result.min -= Math.Abs(__result.max - oldMax);
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(BoneMorph_), nameof(BoneMorph_.Blend))]
			[HarmonyPatch(typeof(TMorphBone), nameof(TMorphBone.Blend))]
			private static IEnumerable<CodeInstruction> UnclampBoneMorphs(IEnumerable<CodeInstruction> instructions)
			{
				var targetMethod = typeof(Vector3).GetMethod(nameof(Vector3.Lerp));
				var replacementMethod = typeof(Vector3).GetMethod(nameof(Vector3.LerpUnclamped));

				var targetMethod2 = typeof(Mathf).GetMethod(nameof(Mathf.Lerp));
				var replacementMethod2 = typeof(Mathf).GetMethod(nameof(Mathf.LerpUnclamped));

				foreach (var codeInstruction in instructions)
				{
					if (!(codeInstruction.operand is MethodInfo methodInfo))
					{
						continue;
					}

					if (methodInfo == targetMethod)
					{
						codeInstruction.operand = replacementMethod;
					} 
					else if (methodInfo == targetMethod2)
					{
						codeInstruction.operand = replacementMethod2;
					}
				}

				return instructions;
			}

			[HarmonyTranspiler]
			[HarmonyPatch(typeof(TMorph), nameof(TMorph.FixBlendValues))]
			[HarmonyPatch(typeof(TMorph), nameof(TMorph.FixBlendValues_Face))]
			private static IEnumerable<CodeInstruction> AllowNegativeMorphs(IEnumerable<CodeInstruction> instructions)
			{
				var codeMatcher = new CodeMatcher(instructions)
					.MatchForward(true, new CodeMatch(new CodeInstruction(OpCodes.Ldc_R4, 0.01f)));
				codeMatcher.Set(OpCodes.Ldc_R4, -1 * _sliderRangeMultiplier.Value);

				return codeMatcher.InstructionEnumeration();
			}
		}
	}
}
