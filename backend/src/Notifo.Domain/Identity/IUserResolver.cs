﻿// ==========================================================================
//  Notifo.io
// ==========================================================================
//  Copyright (c) Sebastian Stehle
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Notifo.Domain.Identity
{
    public interface IUserResolver
    {
        Task<(IUser? User, bool Created)> CreateUserIfNotExistsAsync(string email, bool invited = false,
            CancellationToken ct = default);

        Task<IUser?> FindByIdOrEmailAsync(string idOrEmail,
            CancellationToken ct = default);

        Task<IUser?> FindByIdAsync(string idOrEmail,
            CancellationToken ct = default);

        Task<List<IUser>> QueryByEmailAsync(string email,
            CancellationToken ct = default);

        Task<List<IUser>> QueryAllAsync(
            CancellationToken ct = default);

        Task<Dictionary<string, IUser>> QueryManyAsync(string[] ids,
            CancellationToken ct = default);
    }
}
