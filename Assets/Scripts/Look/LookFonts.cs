using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NightPatrol.Look
{
	/// <summary>
	/// .ttf 글꼴로 실행 중에 TMP 글꼴(동적 아틀라스)을 만들어 나눠 쓴다.
	/// SDF 에셋을 미리 굽지 않아도 되고, 없는 글자는 원래 글꼴(fallback)로 보인다.
	/// </summary>
	public static class LookFonts
	{
		static readonly Dictionary<Font, TMP_FontAsset> cache = new Dictionary<Font, TMP_FontAsset>();

		public static TMP_FontAsset Get(Font font, TMP_FontAsset fallback)
		{
			if (font == null) return null;
			if (cache.TryGetValue(font, out var asset) && asset != null)
			{
				AddFallback(asset, fallback);
				return asset;
			}
			asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
				AtlasPopulationMode.Dynamic, true);
			if (asset == null)
			{
				Debug.LogWarning($"[LookFonts] '{font.name}'로 TMP 글꼴을 만들지 못함 — 글꼴 Import 설정에서 Include Font Data가 켜져 있는지 확인");
				return null;
			}
			asset.name = font.name + " (runtime)";
			AddFallback(asset, fallback);
			cache[font] = asset;
			return asset;
		}

		static void AddFallback(TMP_FontAsset asset, TMP_FontAsset fallback)
		{
			if (fallback == null || fallback == asset) return;
			if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
			if (!asset.fallbackFontAssetTable.Contains(fallback)) asset.fallbackFontAssetTable.Add(fallback);
		}

		/// <summary>씬에 있는 아무 TMP 글자의 글꼴 (없으면 TMP 기본 글꼴)</summary>
		public static TMP_FontAsset SceneDefault()
		{
			var t = Object.FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
			if (t != null && t.font != null) return t.font;
			return TMP_Settings.defaultFontAsset;
		}
	}
}
