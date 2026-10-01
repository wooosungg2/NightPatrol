namespace NightPatrol
{
	/// <summary>점검 사물 Id 규칙 (구역.종류.번호) 도우미. 예: 521 → R21, Stair-W → StairW, Balcony_W_Door → BalconyW</summary>
	public static class PatrolIds
	{
		public static string Zone(string label)
		{
			if (string.IsNullOrEmpty(label)) return "Unknown";
			if (label.Length == 3 && char.IsDigit(label[0]) && char.IsDigit(label[1]) && char.IsDigit(label[2]))
				return "R" + label.Substring(1);                 // 521 → R21 (층은 씬이 정함)
			if (label.EndsWith("_Door")) label = label.Substring(0, label.Length - 5);
			var sb = new System.Text.StringBuilder();
			foreach (char c in label)
				if (char.IsLetterOrDigit(c)) sb.Append(c);       // Stair-W → StairW
			return sb.ToString();
		}

		public static string Make(string label, string kind, int index) => $"{Zone(label)}.{kind}.{index:00}";
	}
}
