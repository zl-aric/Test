using AricTest.Services;
using Microsoft.AspNetCore.Mvc;

namespace AricTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LeaderboardController : ControllerBase
    {
        private readonly LeaderboardService _leaderboardService;

        public LeaderboardController(LeaderboardService leaderboardService)
        {
            _leaderboardService = leaderboardService;
        }

        [HttpPost("customer/{customerid}/score/{score}")]
        public IActionResult UpdateScore(long customerid, decimal score)
        {
            try
            {
                var updatedScore = _leaderboardService.UpdateScore(customerid, score);
                return Ok(updatedScore);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public IActionResult GetByRank([FromQuery] int start, [FromQuery] int end)
        {
            try
            {
                var result = _leaderboardService.GetByRank(start, end);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{customerid}")]
        public IActionResult GetCustomerWithNeighbors(
            long customerid,
            [FromQuery] int high = 0,
            [FromQuery] int low = 0)
        {
            try
            {
                var result = _leaderboardService.GetCustomerWithNeighbors(customerid, high, low);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
