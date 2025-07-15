using AricSortedSet.Services;
using Microsoft.AspNetCore.Mvc;

namespace AricSortedSet.Controllers
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
        public IActionResult AddOrUpdate(long customerid, decimal score)
        {
            try
            {
                var updatedScore = _leaderboardService.AddOrUpdate(customerid, score);
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