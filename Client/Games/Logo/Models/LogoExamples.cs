using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Games.Logo.Models
{
    public static class LogoExamples
    {
        public static List<LogoProgram> Programs { get; } = new List<LogoProgram>
        {
            new LogoProgram
            {
                Name = "Simple Square",
                Description = "Basic square to test Logo functionality",
                Code = @"repeat 4 [
  fd 100
  rt 90
]",
                Tags = new List<string> { "basic", "square", "beginner" }
            },
            
            new LogoProgram
            {
                Name = "Growing Spiral ?",
                Description = "Classic growing spiral - perfect for performance testing!",
                Code = @"for i 1 50 [
  fd :i
  rt 91
]",
                Tags = new List<string> { "spiral", "performance", "classic", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Fast Spiral Performance ??",
                Description = "High-performance spiral with 100 segments",
                Code = @"for i 1 100 [
  fd :i
  rt 89
]",
                Tags = new List<string> { "spiral", "performance", "fast", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Dense Spiral Performance ??",
                Description = "Very dense spiral with 200 growing segments",
                Code = @"for i 1 200 [
  fd :i
  rt 91.5
]",
                Tags = new List<string> { "spiral", "dense", "performance", "stress-test", "growing" }
            },
            
            new LogoProgram
            {
                Name = "MEGA Spiral Performance ??",
                Description = "Ultimate spiral performance test - 500 growing segments!",
                Code = @"for i 1 500 [
  fd :i
  rt 89.9
]",
                Tags = new List<string> { "spiral", "mega", "performance", "stress-test", "ultimate", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Double Spiral",
                Description = "Two interleaved growing spirals",
                Code = @"for i 1 75 [
  fd :i
  rt 91
  fd :i
  rt 89
]",
                Tags = new List<string> { "spiral", "double", "performance", "complex" }
            },
            
            new LogoProgram
            {
                Name = "Rainbow Spiral ??",
                Description = "Growing spiral that changes colors",
                Code = @"setpencolor ""red""
for i 1 40 [
  fd :i
  rt 91
]
setpencolor ""green""
for i 1 40 [
  fd :i
  rt 91
]
setpencolor ""blue""
for i 1 40 [
  fd :i
  rt 91
]",
                Tags = new List<string> { "spiral", "colorful", "growing", "performance" }
            },
            
            new LogoProgram
            {
                Name = "Fixed Dense Spiral",
                Description = "Dense spiral with many small fixed segments",
                Code = @"repeat 1000 [
  fd 0.5
  rt 89.8
]",
                Tags = new List<string> { "spiral", "dense", "performance", "stress-test" }
            },
            
            new LogoProgram
            {
                Name = "Nested Squares Growing",
                Description = "Many nested squares rotating outward",
                Code = @"for i 1 50 [
  repeat 4 [ fd :i rt 90 ]
  fd 5
  rt 7
]",
                Tags = new List<string> { "squares", "nested", "performance", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Starburst Growing",
                Description = "Radiating lines with growing lengths",
                Code = @"for i 1 72 [
  fd :i
  bk :i
  rt 5
]",
                Tags = new List<string> { "star", "radial", "performance", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Complex Mandala",
                Description = "Complex pattern with variable sizes - ultimate test!",
                Code = @"for size 1 20 [
  repeat 18 [
    fd :size
    rt 10
    fd :size
    rt 10
  ]
  rt 18
]",
                Tags = new List<string> { "mandala", "complex", "performance", "stress-test", "ultimate" }
            },
            
            new LogoProgram
            {
                Name = "Speed Test Ultimate ?",
                Description = "Maximum performance test - thousands of operations!",
                Code = @"for i 1 100 [
  repeat 10 [
    fd :i
    rt 36
  ]
  rt 3.6
]",
                Tags = new List<string> { "speed-test", "performance", "stress-test", "ultimate", "mega" }
            },
            
            new LogoProgram
            {
                Name = "Triangle",
                Description = "Simple triangle",
                Code = @"repeat 3 [
  fd 100
  rt 120
]",
                Tags = new List<string> { "basic", "triangle", "beginner" }
            },
            
            new LogoProgram
            {
                Name = "Hexagon",
                Description = "Regular hexagon",
                Code = @"repeat 6 [
  fd 80
  rt 60
]",
                Tags = new List<string> { "basic", "hexagon", "beginner" }
            },
            
            new LogoProgram
            {
                Name = "Star",
                Description = "Five-pointed star",
                Code = @"repeat 5 [
  fd 100
  rt 144
]",
                Tags = new List<string> { "basic", "star", "beginner" }
            },
            
            new LogoProgram
            {
                Name = "Flower Pattern",
                Description = "Flower made of squares",
                Code = @"repeat 8 [
  repeat 4 [
    fd 50
    rt 90
  ]
  rt 45
]",
                Tags = new List<string> { "flower", "pattern", "intermediate" }
            }
        };
    }
}