using WordScapeBlazorWasm.Models;

namespace WordScapeBlazorWasm.Games.Logo.Models
{
    public static class LogoExamples
    {
        public static List<LogoProgram> Programs { get; } = new List<LogoProgram>
        {
new LogoProgram
    {
        Name = "Zigzag Scanner 📺🌈",
        Description = "Scan from top-left to bottom-right like a TV scan line! Goes left-to-right, down, right-to-left, down, with color changing each step.",
        Code = @"; TV-style scan pattern with rainbow colors
; Start at top-left (canvas is 500x500, center is 250,250)
pu
setxy 50 50
pd
seth 90

; Color counter for smooth color transitions
let colorstep 0

; Do multiple scan lines
for line 1 20 [
  ; Scan right (even lines)
  for step 1 40 [
    let colorstep :colorstep + 1
    setpencolor :colorstep
    fd 10
  ]
  
  ; Move down
  rt 90
  fd 10
  rt 90
  
  ; Scan left (odd lines) 
  for step 1 40 [
    let colorstep :colorstep + 1
    setpencolor :colorstep
    fd 10
  ]
  
  ; Move down for next line
  lt 90
  fd 10
  lt 90
]

showstatus ""Scan complete!""",
        Tags = new List<string> { "zigzag", "scanner", "rainbow", "colorful", "pattern", "tv-scan", "raster" }
    },

new LogoProgram
    {
       Name = "Classic Spirals 🌀♾️ (AUTO-START)",
       Description = "THE REAL ORIGINAL! Watch the turtle draw multiple beautiful spirals in real-time! Growing steps, incrementing angles, and auto-clear for the classic infinite effect!",
       Code = @"; Classic Calvin Hsia Logo - Just like the original ""fr+cd."" 
; Creates multiple beautiful spirals, each with a different angle
; Each spiral draws until complete, then clears and draws the next one
; This recreates the infinite cycling effect of the original!

for angle 0 360 [
  ; Draw one complete spiral with this angle
  ; Growing steps (scaled 3x) + color cycling = beautiful!
  for step 1 60 [
    setpencolor :step
    ; Scale step by 3 for better visibility (3-180 pixels)
    fd :step
    fd :step
    fd :step
    rt :angle
    delay 1
  ]
  showstatus : angle
  
  ; Clear screen and start next spiral with new angle
  ; (Just like original: spiral completes, clear, angle++, repeat)
  cs
  home
]

; Creates 52 spirals with angles 85° through 136°!
; Each spiral clears before the next, showing one at a time
; Original ran forever with angle wrapping 0-360 infinitely!
; Try changing angle range for more/fewer spirals",
  Tags = new List<string> { "classic", "auto-start", "original", "infinite", "spirals", "colorful", "growing", "angle-increment" }
},

            new LogoProgram
            {
                Name = "Waveform Patterns 🌊📊",
                Description = "Draw various horizontal waveform patterns: square wave, sawtooth, triangle wave, and sine-like pattern with color iteration",
                Code = @"; Waveform Pattern Generator
; Draws multiple colorful waveforms stacked vertically

; Setup
pu
setxy 0 50
pd

; Square Wave Pattern (Top)
setpencolor 1  ; Red
for wave 1 10 [
  ; Up
  seth 0
  fd 15
  ; Right
  seth 90
  fd 20
  ; Down
  seth 180
  fd 15
  ; Right
  seth 90
  fd 20
]

; Move down for next wave
pu
setxy 0 100
seth 90
pd

; Sawtooth Wave (Second row)
for color 2 4 [
  setpencolor :color
  for tooth 1 8 [
    ; Ramp up
    seth 45
    fd 28
    ; Drop down vertical
    pu
    seth 180
    fd 20
    seth 90
    fd 20
    pd
  ]
  ; Move down for color variation
  pu
  setxy 0 150
  seth 90
  pd
]

; Move down for triangle wave
pu
setxy 0 200
seth 90
pd

; Triangle Wave (Third row)
setpencolor 5  ; Magenta
for tri 1 8 [
  ; Up slope
  seth 45
  fd 28
  ; Down slope
  seth 135
  fd 28
]

; Move down for sine-like wave
pu
setxy 0 250
seth 90
pd

; Sine-like Wave using small steps (Fourth row)
for color 6 9 [
  setpencolor :color
  pu
  setxy 0 250
  pd
  
  ; Create smooth sine-like curve with small angles
  for step 1 40 [
    seth 70  ; Slight up
    fd 5
    seth 90  ; Level
    fd 5
    seth 110 ; Slight down
    fd 5
    seth 90  ; Level
    fd 5
  ]
  
  ; Offset for next color
  pu
  seth 180
  fd 10
  pd
]

; Final decorative burst
pu
setxy 400 150
pd
for ray 0 15 [
  setpencolor :ray
  fd 30
  bk 30
  rt 22.5
]

showstatus ""Waveforms Complete!""",
                Tags = new List<string> { "waveform", "patterns", "educational", "colorful", "waves", "square-wave", "sawtooth", "triangle", "sine", "oscilloscope" }
            },

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
                Name = "Animated Square with Delay ⏱️",
                Description = "Square drawn with delays between each side - great for watching the turtle move!",
                Code = @"; Draw a square with 500ms delay between each side
repeat 4 [
  fd 100
  delay 500
  rt 90
  delay 500
]",
                Tags = new List<string> { "basic", "square", "animated", "delay", "beginner" }
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
            },

new LogoProgram
    {
        Name = "CS Test - Two Squares",
        Description = "Simple test: draw square, clear, draw another square",
        Code = @"; Draw first square
repeat 4 [fd 100 rt 90]
delay 500
; Clear screen
cs
delay 500
; Draw second square
repeat 4 [fd 100 rt 90]",
        Tags = new List<string> { "test", "cs", "clear", "simple" }
    }
        };
    }
}