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
                Name = "Growing Spiral 🌀",
                Description = "Classic growing spiral - perfect for performance testing!",
                Code = @"for i 1 50 [
  fd :i
  rt 91
]",
                Tags = new List<string> { "spiral", "performance", "classic", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Fast Spiral Performance 🚀🌀",
                Description = "High-performance spiral with 100 segments",
                Code = @"for i 1 100 [
  fd :i
  rt 89
]",
                Tags = new List<string> { "spiral", "performance", "fast", "growing" }
            },
            
            new LogoProgram
            {
                Name = "Dense Spiral Performance 🚀🌀",
                Description = "Very dense spiral with 200 growing segments",
                Code = @"for i 1 200 [
  fd :i
  rt 91.5
]",
                Tags = new List<string> { "spiral", "dense", "performance", "stress-test", "growing" }
            },
            
            new LogoProgram
            {
                Name = "MEGA Spiral Performance 🚀🌀",
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
                Name = "Rainbow Spiral 🌈🌀",
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
                Name = "Color Variable Spiral 🎨🌀",
                Description = "Spiral that changes color using integer color variables!",
                Code = @"; Use color variable that changes each segment
for color 0 15 [
  setpencolor :color
  for i 1 8 [
    fd :i
    rt 91
  ]
]",
                Tags = new List<string> { "spiral", "colorful", "variables", "integer-colors", "advanced" }
            },

            new LogoProgram
            {
                Name = "RGB Consecutive Demo 🔴🟢🔵",
                Description = "Shows Red=1, Green=2, Blue=3 as consecutive integers",
                Code = @"; Demonstrate consecutive RGB integers
; Red = 1, Green = 2, Blue = 3
setpencolor 1
repeat 4 [fd 60 rt 90]
setpencolor 2  
repeat 4 [fd 70 rt 90]
setpencolor 3
repeat 4 [fd 80 rt 90]

; Show them in a for loop
for rgb 1 3 [
  setpencolor :rgb
  fd 100
  bk 100
  rt 120
]",
                Tags = new List<string> { "rgb", "consecutive", "demo", "primary-colors", "integers" }
            },

            new LogoProgram
            {
                Name = "Integer Color Demo 🎨",
                Description = "Shows consecutive integer colors: 0=Black, 1=Red, 2=Green, 3=Blue, etc.",
                Code = @"; Draw squares showing consecutive color integers
; 0=Black, 1=Red, 2=Green, 3=Blue, 4=Yellow, 5=Magenta, 6=Cyan, 7=White
for color 0 15 [
  setpencolor :color
  repeat 4 [
    fd 30
    rt 90
  ]
  fd 35
  rt 22.5
]",
                Tags = new List<string> { "color-demo", "integer-colors", "educational", "squares", "consecutive" }
            },

            new LogoProgram
            {
                Name = "Animated Color Spiral 🌈✨",
                Description = "Beautiful spiral with smooth color transitions",
                Code = @"; Creates a spiral where each segment changes color
for segment 1 60 [
  ; Use modulo to cycle through colors 0-15
  for colorstep 0 3 [
    setpencolor :colorstep
    fd :segment
    rt 91
  ]
]",
                Tags = new List<string> { "spiral", "animation", "colorful", "smooth-transition", "advanced" }
            },

            new LogoProgram
            {
                Name = "Color Burst Star ⭐🎨",
                Description = "Star pattern with different colored rays",
                Code = @"; Draw a star with different colored rays
for ray 0 11 [
  setpencolor :ray
  fd 80
  bk 80
  rt 30
]",
                Tags = new List<string> { "star", "radial", "colorful", "integer-colors", "burst" }
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
                Name = "Speed Test Ultimate 🚀",
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
                Name = "Color Names Demo 📝🎨",
                Description = "Shows that color names like 'Red' work as integers",
                Code = @"; Demonstrate that color names translate to integers
setpencolor ""Red""
repeat 4 [fd 50 rt 90]
setpencolor ""Blue""  
repeat 4 [fd 60 rt 90]
setpencolor ""Green""
repeat 4 [fd 70 rt 90]
setpencolor ""Yellow""
repeat 4 [fd 80 rt 90]",
                Tags = new List<string> { "color-names", "demo", "educational", "squares", "translation" }
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