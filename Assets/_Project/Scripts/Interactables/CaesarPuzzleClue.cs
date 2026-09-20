public static class CaesarPuzzleClue
{
    public const string Solution = "SILENT ORBIT";
    public const string Instructions = "Start with A aligned with A. Turn the inner ring clockwise one notch for each time you pressed green in Simon’s final round. Find each coded letter on the outer ring and read the aligned inner letter. Return to Simon to watch the final sequence again.";

    public static string ForSimon(SimonSaysController simon)
    {
        if (simon == null) throw new System.ArgumentNullException(nameof(simon));
        string encoded = CaesarCipherMath.ShiftSymbols(Solution, simon.FinalGreenCount);
        return "CAESAR'S NOTE\n\nDECODE THIS MESSAGE:\n" + encoded
            + "\n\nHOW TO USE THE WHEEL\n" + Instructions
            + "\n\nEnter the decoded words at the terminal.";
    }
}
