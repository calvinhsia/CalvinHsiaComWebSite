using System.Linq;
using System.Threading.Tasks;
using System;
using WordScapeBlazorWasm.Services;

public class WordHandler
{
    public static WordHandler? Instance;
    private readonly IDictionaryService? _dictionaryService;
    public DictionaryLib.DictionaryLib _dict;
    Random _random;
    List<string> candidateWords = new List<string>();
    
    // Constructor for dependency injection (preferred)
    public WordHandler(IDictionaryService dictionaryService, Random? srandom = null)
    {
        _dictionaryService = dictionaryService;
        _dict = dictionaryService.SmallDictionary; // Use shared instance
        InitializeWordHandler(srandom);
        DebugHelper.Log("WordHandler: Using shared DictionaryService instance");
    }
    
    // Legacy constructor for backward compatibility
    public WordHandler(Random? srandom = null)
    {
        _dictionaryService = null; // Explicitly set to null for clarity
        _dict = new DictionaryLib.DictionaryLib(DictionaryLib.DictionaryType.Small);
        DebugHelper.LogWarning("WordHandler: Creating new DictionaryLib instance (expensive operation)");
        InitializeWordHandler(srandom);
    }
    
    private void InitializeWordHandler(Random? srandom)
    {
        if (srandom != null)
        {
            _random = srandom;
        }
        else
        {
            _random = new Random();
        }
        Instance = this;
        
        // Initialize candidate words after dictionary is set
        if (_dict != null)
        {
            candidateWords = _dict.GetAllWords().Where(w => w.Length >= 10 && w.Length <= nRows * nCols).ToList();
        }
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
        return _dict.RandomWord();
    }
    int nRows = 4;
    int nCols = 4;
    public (string randWord, string grid, string gridFilledWithRand) CreateGrid()
    {
        var directions = Enumerable.Range(0, 8).ToArray();
        var randWord = string.Empty;
        var resGrid = string.Empty;
        var resGridFilledWithRand = string.Empty;
        var isGood = false;
        while (!isGood)
        {
            randWord = candidateWords[_random.Next(candidateWords.Count)];
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
                directions = directions.OrderBy(x => _random!.Next()).ToArray();
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
            recurlam(_random!.Next(nRows), _random!.Next(nCols), 0);
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

        foreach (var let in resGrid)
        {
            if (let == '_')
            {
                resGridFilledWithRand += (char)(65 + _random.Next(26));
            }
            else
            {
                resGridFilledWithRand += let;
            }
        }
        return (randWord, resGrid, resGridFilledWithRand);
    }
}

