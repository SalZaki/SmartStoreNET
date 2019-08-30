﻿using System.Collections.Generic;
using SmartStore.Core.Domain.Security;

namespace SmartStore.Services.Security
{
    public interface IPermissionProvider2
    {
        IEnumerable<PermissionRecord> GetPermissions();
        IEnumerable<DefaultPermissionRecord> GetDefaultPermissions();
    }
}
