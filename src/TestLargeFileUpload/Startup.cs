﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Authentication.Facebook;
using Microsoft.AspNet.Authentication.Google;
using Microsoft.AspNet.Authentication.MicrosoftAccount;
using Microsoft.AspNet.Authentication.Twitter;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Diagnostics.Entity;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Http;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Data.Entity;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.FileSystem;
using Raven.Client.Indexes;

namespace TestLargeFileUpload
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IApplicationEnvironment appEnv)
        {
            // Setup configuration sources.

            var builder = new ConfigurationBuilder(appEnv.ApplicationBasePath)
                .AddJsonFile("config.json")
                .AddJsonFile($"config.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // This reads the configuration keys from the secret store.
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add MVC services to the services container.
            services.AddMvc();
            
            services.AddSingleton(p =>
            {
                var documentStore = new DocumentStore()
                {
                    Url = Configuration["RavenDb:Url"],
                    DefaultDatabase = Configuration["RavenDb:DefaultDatabase"]
                }.Initialize();

                IndexCreation.CreateIndexes(this.GetType().Assembly, documentStore);
                return documentStore;
            });
            services.AddSingleton(p =>
            {
                var fsStore = new FilesStore()
                {
                    Url = Configuration["RavenDb:Url"],
                    DefaultFileSystem = Configuration["RavenDb:DefaultFilesStore"]
                }.Initialize();
                return fsStore;
            });


        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.MinimumLevel = LogLevel.Information;
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();

            // Configure the HTTP request pipeline.

            // Add the following to the request pipeline only in development environment.
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseErrorPage();
                app.UseDatabaseErrorPage(DatabaseErrorPageOptions.ShowAll);
            }
            else
            {
                // Add Error handling middleware which catches all application specific errors and
                // sends the request to the following path or controller action.
                app.UseErrorHandler("/Home/Error");
            }

            // Add static files to the request pipeline.
            app.UseStaticFiles();

            // Add MVC to the request pipeline.
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");

                // Uncomment the following line to add a route for porting Web API 2 controllers.
                // routes.MapWebApiRoute("DefaultApi", "api/{controller}/{id?}");
            });

            app.UseMiddleware<RavenDbMiddleware>();

        }

        public class RavenDbMiddleware
        {
            private readonly RequestDelegate _next;

            public RavenDbMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public async Task Invoke(HttpContext context)
            {
                await _next(context);

                var asyncSession = context.RequestServices.GetService<IAsyncDocumentSession>();
                if (asyncSession?.Advanced.HasChanges ?? false)
                    await asyncSession.SaveChangesAsync();
                var session = context.RequestServices.GetService<IDocumentSession>();
                if (session?.Advanced.HasChanges ?? false)
                    session.SaveChanges();
            }
        }

    }
}
