namespace AudioLoadRemover
{
    public class TimeSpanFormatter
    {
        public static string ToShortString(TimeSpan t)
        {
            if (t.TotalMinutes < 1.0)
            {
                return t.ToString("s\\.ff");
            }
            else if (t.TotalHours < 1.0)
            {
                return t.ToString("m\\:ss\\.ff");
            }
            else
            {
                return t.ToString("h\\:mm\\:ss\\.ff");
            }
        }
    }
}
