using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WordScapeBlazorWasm.Models;

namespace TestProject1
{
    [TestClass]
    public class TestWordScape
    {
        [TestMethod]
        public async Task TestGridGeneration()
        {
            var random = new Random();
            for (int i = 0; i < 3; i++)
            {
                var x = random.Next();
            }
            var parms = new WordGenerationParms()
            {
                _Random = random,
                LenTargetWord = 7,
                MinSubWordLength = 3
            };
            var puz = await WordScapePuzzle.CreateNextPuzzleTask(parms);
            var grid = puz.genGrid;
            Console.WriteLine(puz.wordContainer.InitialWord);
            foreach (var x in grid!._dictPlacedWords)
            {
                Console.WriteLine($"Placed word {x.Key} at {x.Value.nX},{x.Value.nY} horiz={x.Value.IsHoriz}");
            }
            // dump the grid
            for (int y = 0; y < grid!._MaxY; y++)
            {
                var sb = new StringBuilder();
                for (int x = 0; x < grid!._MaxX; x++)
                {
                    sb.Append(grid!._chars[x, y]);
                    sb.Append('_');
                }
                Console.WriteLine(sb.ToString());
            }






        }
    }
}
