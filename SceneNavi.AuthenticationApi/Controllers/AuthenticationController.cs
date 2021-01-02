using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SceneNavi.AuthenticationApi.Controllers
{
 
       [Route("[controller]")]
          [ApiController]
          public class AuthenticationController : ControllerBase
          {
              private readonly UserManager<IdentityUser> _userManager;
              private readonly SignInManager<IdentityUser> _signInManager;
      
              public AuthenticationController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
              {
                  _userManager = userManager;
                  _signInManager = signInManager;
              }
      
      
      
              [HttpGet("[action]")]
              public async Task<IdentityUser> Login(string userName)
              {
                  return await _userManager.FindByEmailAsync(userName);
              }
      
      
              [EnableCors("CorsPolicy")]
              [HttpGet("register/{username}/{email}")]
              public async Task<IdentityResult> Register([FromRoute] string username, [FromRoute] string email)
              {
                  var user = new IdentityUser
                  {
                      UserName = username,
                      Email = email
                  };
                  return await _userManager.CreateAsync(user);
              }

    }
}