﻿/*
 * Copyright 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Autofac;
using Autofac.Integration.WebApi;
using Microsoft.Owin;
using System;
using Thinktecture.IdentityServer.Core.Configuration;
using Thinktecture.IdentityServer.Core.Logging;
using Thinktecture.IdentityServer.Core.Services;
using Thinktecture.IdentityServer.WsFederation.Configuration.Hosting;
using Thinktecture.IdentityServer.WsFederation.Hosting;
using Thinktecture.IdentityServer.WsFederation.ResponseHandling;
using Thinktecture.IdentityServer.WsFederation.Validation;

namespace Thinktecture.IdentityServer.WsFederation.Configuration
{
    internal static class AutofacConfig
    {
        static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        public static IContainer Configure(WsFederationPluginOptions options)
        {
            if (options == null) throw new ArgumentNullException("options");

            var factory = options.Factory;
            factory.Validate();

            var builder = new ContainerBuilder();

            // mandatory from factory
            builder.Register(factory.UserService);
            builder.Register(factory.RelyingPartyService);

            // validators
            builder.RegisterType<SignInValidator>().AsSelf();

            // processors
            builder.RegisterType<SignInResponseGenerator>().AsSelf();
            builder.RegisterType<MetadataResponseGenerator>().AsSelf();
            
            // general services
            builder.RegisterType<CookieMiddlewareTrackingCookieService>().As<ITrackingCookieService>();
            builder.RegisterInstance(options).AsSelf();
            builder.RegisterInstance(options.IdentityServerOptions).AsSelf();

            // load core controller
            builder.RegisterApiControllers(typeof(WsFederationController).Assembly);

            builder.Register<OwinEnvironmentService>(resolver => new OwinEnvironmentService(resolver.Resolve<IOwinContext>().Environment));

            // register additional dependencies from identity server
            foreach (var registration in options.IdentityServerOptions.Factory.Registrations)
            {
                builder.Register(registration);
            }

            // add any additional dependencies from hosting application
            foreach (var registration in factory.Registrations)
            {
                builder.Register(registration, registration.Name);
            }

            return builder.Build();
        }

        private static void Register(this ContainerBuilder builder, Registration registration, string name = null)
        {
            if (registration.Instance != null)
            {
                var reg = builder.Register(ctx => registration.Instance).SingleInstance();
                if (name != null)
                {
                    reg.Named(name, registration.DependencyType);
                }
                else
                {
                    reg.As(registration.DependencyType);
                }
            }
            else if (registration.Type != null)
            {
                var reg = builder.RegisterType(registration.Type);
                if (name != null)
                {
                    reg.Named(name, registration.DependencyType);
                }
                else
                {
                    reg.As(registration.DependencyType);
                }
            }
            else if (registration.Factory != null)
            {
                var reg = builder.Register(ctx => registration.Factory(new AutofacDependencyResolver(ctx)));
                if (name != null)
                {
                    reg.Named(name, registration.DependencyType);
                }
                else
                {
                    reg.As(registration.DependencyType);
                }
            }
            else
            {
                var message = "No type or factory found on registration " + registration.GetType().FullName;
                Logger.Error(message);
                throw new InvalidOperationException(message);
            }
        }
    }
}