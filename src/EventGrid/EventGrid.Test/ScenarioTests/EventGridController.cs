﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.Common.Authentication;
using Microsoft.Azure.Test.HttpRecorder;
using Microsoft.Rest.ClientRuntime.Azure.TestFramework;
using Microsoft.WindowsAzure.Commands.ScenarioTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Azure.Management.EventGrid;
using TestEnvironmentFactory = Microsoft.Rest.ClientRuntime.Azure.TestFramework.TestEnvironmentFactory;
using Microsoft.Azure.Management.EventHub;
using Microsoft.Azure.Management.Internal.Resources;
using Microsoft.Azure.ServiceManagement.Common.Models;
using Microsoft.Azure.Management.Relay;
using Microsoft.Azure.Management.ServiceBus;
using Microsoft.Azure.Management.Storage;

namespace Microsoft.Azure.Commands.EventGrid.Test.ScenarioTests
{
    public class EventGridController
    {
        private readonly EnvironmentSetupHelper _helper;

        public ResourceManagementClient ResourceManagementClient { get; private set; }

        public EventGridManagementClient EventGridManagementClient { get; private set; }

        public EventHubManagementClient EventHubClient { get; private set; }

        public RelayManagementClient RelayManagementClient { get; private set; }

        public ServiceBusManagementClient ServiceBusManagementClient { get; private set; }

        public StorageManagementClient StorageManagementClient { get; private set; }

        public string UserDomain { get; private set; }

        public static EventGridController NewInstance => new EventGridController();

        public EventGridController()
        {
            _helper = new EnvironmentSetupHelper();
        }

        public void RunPsTest(XunitTracingInterceptor logger, params string[] scripts)
        {
            var sf = new StackTrace().GetFrame(1);
            var callingClassType = sf.GetMethod().ReflectedType?.ToString();
            var mockName = sf.GetMethod().Name;

            _helper.TracingInterceptor = logger;

            RunPsTestWorkflow(
                () => scripts,
                // no custom cleanup
                null,
                callingClassType,
                mockName);
        }

        public void RunPsTestWorkflow(
            Func<string[]> scriptBuilder,
            Action cleanup,
            string callingClassType,
            string mockName)
        {
            var d = new Dictionary<string, string>
            {
                {"Microsoft.Resources", null},
                {"Microsoft.Features", null},
                {"Microsoft.Authorization", null},
                {"Microsoft.EventHub", null}
            };
            var providersToIgnore = new Dictionary<string, string>
            {
                {"Microsoft.Azure.Management.Resources.ResourceManagementClient", "2016-02-01"}
            };
            HttpMockServer.Matcher = new PermissiveRecordMatcherWithApiExclusion(true, d, providersToIgnore);

            HttpMockServer.RecordsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SessionRecords");
            using (var context = MockContext.Start(callingClassType, mockName))
            {
                SetupManagementClients(context);
                _helper.SetupEnvironment(AzureModule.AzureResourceManager);

                var callingClassName = callingClassType.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries).Last();
                _helper.SetupModules(AzureModule.AzureResourceManager,
                    "ScenarioTests\\Common.ps1",
                    "ScenarioTests\\" + callingClassName + ".ps1",
                    _helper.RMProfileModule,
                    _helper.GetRMModulePath(@"AzureRM.EventHub.psd1"),
                    _helper.GetRMModulePath(@"AzureRM.EventGrid.psd1"),
                    "AzureRM.Resources.ps1",
                    _helper.GetRMModulePath(@"Az.Relay.psd1"),
                    _helper.GetRMModulePath(@"Az.Storage.psd1"),
                    _helper.GetRMModulePath(@"Az.ServiceBus.psd1"));

                try
                {
                    var psScripts = scriptBuilder?.Invoke();
                    if (psScripts != null)
                    {
                        _helper.RunPowerShellTest(psScripts);
                    }
                }
                finally
                {
                    cleanup?.Invoke();
                }
            }
        }

        private void SetupManagementClients(MockContext context)
        {
            ResourceManagementClient = GetResourceManagementClient(context);
            EventGridManagementClient = GetEventGridManagementClient(context);
            EventHubClient = GetEHClient(context);
            RelayManagementClient = GetRelayManagementClient(context);
            ServiceBusManagementClient = GetServiceBusManagementClient(context);
            StorageManagementClient = GetStorageManagementClient(context);
            _helper.SetupManagementClients(StorageManagementClient, ServiceBusManagementClient, RelayManagementClient, EventHubClient, ResourceManagementClient, EventGridManagementClient);
        }

        private static ResourceManagementClient GetResourceManagementClient(MockContext context)
        {
            return context.GetServiceClient<ResourceManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static EventGridManagementClient GetEventGridManagementClient(MockContext context)
        {
            return context.GetServiceClient<EventGridManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static EventHubManagementClient GetEHClient(MockContext context)
        {
            return context.GetServiceClient<EventHubManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static RelayManagementClient GetRelayManagementClient(MockContext context)
        {
            return context.GetServiceClient<RelayManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static ServiceBusManagementClient GetServiceBusManagementClient(MockContext context)
        {
            return context.GetServiceClient<ServiceBusManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }

        private static StorageManagementClient GetStorageManagementClient(MockContext context)
        {
            return context.GetServiceClient<StorageManagementClient>(TestEnvironmentFactory.GetTestEnvironment());
        }
    }
}
