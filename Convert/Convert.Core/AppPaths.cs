namespace Convert.Core
{
    public static class AppPaths
    {
        public static string ReportsFolder
        {
            get
            {
                string doc = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string folder = Path.Combine(doc, "Convert", "Reports");

                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static string GetReportPath(string baseName)
        {
            string fileName = $"{baseName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";
            return Path.Combine(ReportsFolder, fileName);
        }
    }

}
