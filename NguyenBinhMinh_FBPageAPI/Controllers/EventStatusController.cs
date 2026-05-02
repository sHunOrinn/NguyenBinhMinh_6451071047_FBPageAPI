using Microsoft.AspNetCore.Mvc;
using NguyenBinhMinh_FBPageAPI.Services;

namespace NguyenBinhMinh_FBPageAPI.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventStatusController : ControllerBase
    {
        private readonly EventStateStore _eventStateStore;

        public EventStatusController(EventStateStore eventStateStore)
        {
            _eventStateStore = eventStateStore;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_eventStateStore.GetAll());
        }

        [HttpGet("{eventId}")]
        public IActionResult GetById(string eventId)
        {
            var result = _eventStateStore.Get(eventId);

            if (result == null)
            {
                return NotFound(new
                {
                    message = "Event not found"
                });
            }

            return Ok(result);
        }
    }
}