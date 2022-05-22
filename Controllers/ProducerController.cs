using DvD_Api.Data;
using DvD_Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DvD_Api.Controllers
{
    [ApiController]
    [Route("api/[Controller]")]
    [Authorize]
    public class ProducerController:ControllerBase
    {
        public readonly ApplicationDbContext _db;

        public ProducerController(ApplicationDbContext database)
        {
            _db = database;
        }

        [HttpGet]
        public IEnumerable<Producer> GetAllProducer() { 
            return _db.Producers;
        }
    }
}
