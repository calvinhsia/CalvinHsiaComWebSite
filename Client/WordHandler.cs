using System.Linq;
using System.Threading.Tasks;
using System;


public class WordHandler
{
    public static WordHandler? Instance;
    public DictionaryLib.DictionaryLib dict;
    Random random = new Random();
    public WordHandler()
    {
        dict = new DictionaryLib.DictionaryLib(DictionaryLib.DictionaryType.Small);
        Instance = this;
    }
    public async Task<string?> GetData()
    {
        var addr = "https://calvinhvscode.azurewebsites.net/api/GetWordData";
        var url = new Uri(addr);
        var cl = new HttpClient();
        var res = await cl.GetAsync(url);
        var str = await res.Content.ReadAsStringAsync();
        return str;
    }
    public string GetRandWord()
    {
        return dict.RandomWord();
    }
    int nRows = 4;
    int nCols = 4;
    public (string randWord, string grid) CreateGrid()
    {
        var dict = new DictionaryLib.DictionaryLib(DictionaryLib.DictionaryType.Small, random: random); // https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-entities?tabs=csharp
        var directions = Enumerable.Range(0, 8).ToArray();
        var randWord = string.Empty;
        var resGrid = string.Empty;
        var isGood = false;
        while (!isGood)
        {
            while (true)
            {
                randWord = dict.RandomWord();
                if (randWord.Length > 9 && randWord.Length < nRows * nCols)
                {
                    break;
                }
            }
            var arrGrid = new char[nRows, nCols];
            Func<int, int, int, bool>? recurlam = null;
            recurlam = (r, c, ndxw) =>
            {
                arrGrid[r, c] = randWord[ndxw];
                if (ndxw == randWord.Length - 1)
                {
                    isGood = true;
                    return true;
                }
                directions = directions.OrderBy(x => random!.Next()).ToArray();
                for (var idir = 0; idir < 7; idir++)
                {
                    isGood = true;
                    var newr = r;
                    var newc = c;
                    switch (directions[idir])
                    {
                        case 0:
                            newr -= 1;
                            newc -= 1;
                            break;
                        case 1:
                            newr -= 1;
                            break;
                        case 2:
                            newr -= 1;
                            newc += 1;
                            break;
                        case 3:
                            newc -= 1;
                            break;
                        case 4:
                            newc += 1;
                            break;
                        case 5:
                            newr += 1;
                            newc -= 1;
                            break;
                        case 6:
                            newr += 1;
                            break;
                        case 7:
                            newr += 1;
                            newc += 1;
                            break;
                    }
                    if (newr < 0 || newr >= nRows || newc < 0 || newc >= nCols)
                    {
                        isGood = false;
                    }
                    else
                    {
                        if (arrGrid[newr, newc] != '\0')
                        {
                            isGood = false;
                        }
                    }
                    if (isGood)
                    {
                        if (recurlam!(newr, newc, ndxw + 1))
                        {
                            break;
                        }
                        else
                        {
                            isGood = false;
                        }
                    }
                }
                if (!isGood)
                {
                    arrGrid[r, c] = '\0';
                }
                return isGood;
            };
            recurlam(random!.Next(nRows), random!.Next(nCols), 0);
            if (isGood)
            {
                for (int i = 0; i < nRows; i++)
                {
                    for (int j = 0; j < nCols; j++)
                    {
                        var c = arrGrid[i, j];
                        if (c == '\0')
                        {
                            c = '_';
                        }
                        resGrid += c;
                    }
                }
                resGrid = resGrid.ToUpper();
            }
        }
        randWord = randWord.ToUpper();
        return (randWord, resGrid);
    }
}

