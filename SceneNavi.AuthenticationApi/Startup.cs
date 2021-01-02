using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

namespace SceneNavi.AuthenticationApi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder.WithOrigins("http://localhost:4200")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials());
            });

            services.AddDbContext<IdentityContext>(config =>
            {
                config.UseInMemoryDatabase("Memory");
                // config.UseSqlite(@"Data Source=IdentityDB.db;");
            });

            services.AddIdentity<IdentityUser, IdentityRole>(config =>
                {
                    config.Password.RequiredLength = 6;
                    config.Password.RequireDigit = false;
                    config.Password.RequireNonAlphanumeric = false;
                    config.Password.RequireUppercase = false;
                })
                .AddDefaultTokenProviders()
                .AddEntityFrameworkStores<IdentityContext>();

            services.ConfigureApplicationCookie(config =>
            {
                config.Cookie.Name = "Erf.Cookie";
                config.LoginPath = "/Authentication/Login";
            });

            //            services.AddAuthentication("CookieAuth").AddCookie("CookieAuth", config =>
            //            {
            //                config.Cookie.Name = "ErfCookie.Cookie";
            //                config.LoginPath = "/Authentication/Authenticate";
            //            });
            //
            //            services.AddAuthorization(config =>
            //            {
            //                config.AddPolicy("Claim.Dob", policyBuilder =>
            //                {
            //                   // policyBuilder.AddRequirements(new CustomRequireClaim(ClaimTypes.DateOfBirth));
            //                });
            //            });


            services.AddControllers();
            services.AddSwaggerGen(swagger => { swagger.SwaggerDoc("v1", new OpenApiInfo {Title = "Auth API V1"}); });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseCors("CorsPolicy");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("v1/swagger.json", "Auth API V1");
                // c.RoutePrefix = string.Empty;
            });
        }
    }
}