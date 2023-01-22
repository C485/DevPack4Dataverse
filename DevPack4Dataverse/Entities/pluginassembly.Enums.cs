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

internal partial class PluginAssembly
{
    public class Enums
    {
        public enum AuthType
        {
            BasicAuth = 0,
        }

        public enum ComponentState
        {
            Published = 0,
            Unpublished = 1,
            Deleted = 2,
            DeletedUnpublished = 3,
        }

        public enum IsManaged
        {
            Managed = 1,
            Unmanaged = 0,
        }

        public enum IsolationMode
        {
            None = 1,
            Sandbox = 2,
            External = 3,
        }

        public enum IsPasswordSet
        {
            Yes = 1,
            No = 0,
        }

        public enum SourceType
        {
            Database = 0,
            Disk = 1,
            Normal = 2,
            AzureWebApp = 3,
            FileStore = 4,
        }
    }
}
