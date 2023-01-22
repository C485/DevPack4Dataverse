/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace DevPack4Dataverse.Entities;

internal partial class SdkMessageProcessingStep
{
    public class Enums
    {
        public enum AsyncAutoDelete
        {
            Yes = 1,
            No = 0,
        }

        public enum CanUseReadOnlyConnection
        {
            Yes = 1,
            No = 0,
        }

        public enum ComponentState
        {
            Published = 0,
            Unpublished = 1,
            Deleted = 2,
            DeletedUnpublished = 3,
        }

        public enum InvocationSource
        {
            Internal = -1,
            Parent = 0,
            Child = 1,
        }

        public enum IsManaged
        {
            Managed = 1,
            Unmanaged = 0,
        }

        public enum Mode
        {
            Synchronous = 0,
            Asynchronous = 1,
        }

        public enum Stage
        {
            InitialPreoperationForinternaluseonly = 5,
            Prevalidation = 10,
            InternalPreoperationBeforeExternalPluginsForinternaluseonly = 15,
            Preoperation = 20,
            InternalPreoperationAfterExternalPluginsForinternaluseonly = 25,
            MainOperationForinternaluseonly = 30,
            InternalPostoperationBeforeExternalPluginsForinternaluseonly = 35,
            Postoperation = 40,
            InternalPostoperationAfterExternalPluginsForinternaluseonly = 45,
            PostoperationDeprecated = 50,
            FinalPostoperationForinternaluseonly = 55,
            PreCommitstagefiredbeforetransactioncommitForinternaluseonly = 80,
            PostCommitstagefiredaftertransactioncommitForinternaluseonly = 90,
        }

        public enum StateCode
        {
            Enabled = 0,
            Disabled = 1,
        }

        public enum StatusCode
        {
            Enabled = 1,
            Disabled = 2,
        }

        public enum SupportedDeployment
        {
            ServerOnly = 0,
            MicrosoftDynamics365ClientforOutlookOnly = 1,
            Both = 2,
        }
    }
}
