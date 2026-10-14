public class ScoreManager : MonoSingleton<ScoreManager>
{
	public static readonly int baseBlockScore = 2;

	private readonly int[] scoreArray = new int[22]
	{
		6,
		6,
		12,
		20,
		12,
		20,
		12,
		20,
		12,
		20,
		24,
		24,
		32,
		52,
		32,
		24,
		52,
		40,
		60,
		80,
		300,
		400
	};

	public int GetScoreUnit(ScoreType scoreType)
	{
		if (scoreType == ScoreType.None)
		{
			return 0;
		}
		return scoreArray[(int)scoreType];
	}
}
