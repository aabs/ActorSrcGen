using Microsoft.AspNetCore.Mvc;

using ActorSrcGen.Abstractions.Playground;

namespace ActorSrcGen.Web.Playground.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CalcController : ControllerBase
    {
        private readonly ILogger<CalcController> _logger;

        public CalcController(ILogger<CalcController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public int GetAsync() => 42;

        [HttpPost("add")]
        public async Task<int> PostAddAsync([FromBody] Payload payload)
        {
            var (a, b) = payload;
            var result = a + b;

            _logger.LogInformation("{a} + {b} = {result}", a, b, result);
            await Task.Delay(TimeSpan.FromSeconds(result % 4));

            return result;
        }

        [HttpPost("subtract")]
        public async Task<int> PostSubtractAsync([FromBody] Payload payload)
        {
            var (a, b) = payload;
            var result = a - b;

            _logger.LogInformation("{a} - {b} = {result}", a, b, result);
            await Task.Delay(TimeSpan.FromSeconds(Math.Abs(result % 4)));

            return result;
        }
    }
}