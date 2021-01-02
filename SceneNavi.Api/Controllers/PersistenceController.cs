using Microsoft.AspNetCore.Mvc;

namespace WebApplication.Controllers
{
    public class PersistenceController : Controller
    {
        [HttpPost("[action]")]
        public IActionResult SaveEntity(object entity)
        {
            return Ok(null);
        }
        
        [HttpPost("[action]")]
        public IActionResult UpdateEntity(object entity)
        {
            return Ok(null);
        }
        
        [HttpPost("[action]")]
        public IActionResult CreateEntity(object entity)
        {
            return Ok(null);
        }
        
        [HttpPost("[action]")]
        public IActionResult DeleteEntity(object entity)
        {
            return Ok(null);
        }
    }
}