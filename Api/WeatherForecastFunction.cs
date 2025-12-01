using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Client.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ApiIsolated
{
    public class WeatherForecastClass
    {
        private readonly ILogger _logger;

        public WeatherForecastClass(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WeatherForecastClass>();
        }

        [Function(nameof(WeatherForecast))]
        public async Task<HttpResponseData> WeatherForecast([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            _logger.LogInformation("Function called: {function} - v2 with await fix", nameof(WeatherForecast));
            var randomNumber = new Random();
            var temp = 0;

            var result = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = temp = randomNumber.Next(-20, 55),
                Summary = GetSummary(temp)
            }).ToArray();

            _logger.LogInformation("Generated {count} weather forecasts", result.Length);
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            await response.WriteAsJsonAsync(result);
            
            _logger.LogInformation("Response created and JSON written");
            return response;
        }

        private string GetSummary(int temp)
        {
            var summary = "Mild";

            if (temp >= 32)
            {
                summary = "Hot";
            }
            else if (temp <= 16 && temp > 0)
            {
                summary = "Cold";
            }
            else if (temp <= 0)
            {
                summary = "Freezing";
            }

            return summary;
        }
    }
}
