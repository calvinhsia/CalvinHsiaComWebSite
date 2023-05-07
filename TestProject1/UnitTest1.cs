namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        public TestContext TestContext { get; set; }
        [TestMethod]
        public void TestMethod1()
        {
            var rand = new Random(1);
            var wh = new WordHandler(rand);
            for (int i = 0; i < 10; i++)
            {
                (var randword, var grid, var gfilled) = wh.CreateGrid();
                Console.WriteLine($"RandWord {randword} {grid}  {gfilled}");
            }
        }
    }
}