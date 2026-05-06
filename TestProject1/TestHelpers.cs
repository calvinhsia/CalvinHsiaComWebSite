using System.IO;

namespace TestProject1
{
    internal static class TestHelpers
    {
        /// <summary>
        /// Walks up the directory tree from the test binary to find the repository root
        /// (the directory that contains a .git folder or a .sln file).
        /// </summary>
        public static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    dir.GetFiles("*.sln").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not find repository root (no .git or .sln found)");
        }
    }
}
