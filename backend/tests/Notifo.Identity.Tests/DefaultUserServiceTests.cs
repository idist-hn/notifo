﻿// ==========================================================================
//  Notifo.io
// ==========================================================================
//  Copyright (c) Sebastian Stehle
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Globalization;
using System.Security.Claims;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Notifo.Domain.Identity;
using Notifo.Infrastructure;
using Xunit;

namespace Notifo.Identity
{
    public class DefaultUserServiceTests
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly CancellationToken ct;
        private readonly UserManager<IdentityUser> userManager = A.Fake<UserManager<IdentityUser>>();
        private readonly IUserFactory userFactory = A.Fake<IUserFactory>();
        private readonly IUserEvents userEvents = A.Fake<IUserEvents>();
        private readonly DefaultUserService sut;

        public DefaultUserServiceTests()
        {
            ct = cts.Token;

            A.CallTo(() => userFactory.IsId(A<string>._))
                .Returns(true);

            A.CallTo(userManager).WithReturnType<Task<IdentityResult>>()
                .Returns(IdentityResult.Success);

            var log = A.Fake<ILogger<DefaultUserService>>();

            sut = new DefaultUserService(userManager, userFactory, Enumerable.Repeat(userEvents, 1), log);
        }

        [Fact]
        public async Task Should_not_resolve_identity_if_id_not_valid()
        {
            var invalidId = "__";

            A.CallTo(() => userFactory.IsId(invalidId))
                .Returns(false);

            var result = await sut.FindByIdAsync(invalidId, ct);

            Assert.Null(result);

            A.CallTo(() => userManager.FindByIdAsync(invalidId))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task Should_return_identity_by_id_if_found()
        {
            var identity = CreateIdentity(found: true);

            var result = await sut.FindByIdAsync(identity.Id, ct);

            Assert.Same(identity, result?.Identity);
        }

        [Fact]
        public async Task Should_return_null_if_identity_by_id_not_found()
        {
            var identity = CreateIdentity(found: false);

            var result = await sut.FindByIdAsync(identity.Id, ct);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_identity_by_email_if_found()
        {
            var identity = CreateIdentity(found: true);

            var result = await sut.FindByEmailAsync(identity.Email, ct);

            Assert.Same(identity, result?.Identity);
        }

        [Fact]
        public async Task Should_return_null_if_identity_by_email_not_found()
        {
            var identity = CreateIdentity(found: false);

            var result = await sut.FindByEmailAsync(identity.Email, ct);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_return_identity_by_login_if_found()
        {
            var provider = "my-provider";
            var providerKey = "key";

            var identity = CreateIdentity(found: true);

            A.CallTo(() => userManager.FindByLoginAsync(provider, providerKey))
                .Returns(identity);

            var result = await sut.FindByLoginAsync(provider, providerKey, ct);

            Assert.Same(identity, result?.Identity);
        }

        [Fact]
        public async Task Should_return_null_if_identity_by_login_not_found()
        {
            var provider = "my-provider";
            var providerKey = "key";

            CreateIdentity(found: false);

            A.CallTo(() => userManager.FindByLoginAsync(provider, providerKey))
                .Returns(Task.FromResult<IdentityUser>(null!));

            var result = await sut.FindByLoginAsync(provider, providerKey, ct);

            Assert.Null(result);
        }

        [Fact]
        public async Task Should_provide_password_existence()
        {
            var identity = CreateIdentity(found: true);

            var user = A.Fake<IUser>();

            A.CallTo(() => user.Identity)
                .Returns(identity);

            A.CallTo(() => userManager.HasPasswordAsync(identity))
                .Returns(true);

            var result = await sut.HasPasswordAsync(user, ct);

            Assert.True(result);
        }

        [Fact]
        public async Task Should_provide_logins()
        {
            var logins = new List<UserLoginInfo>();

            var identity = CreateIdentity(found: true);

            var user = A.Fake<IUser>();

            A.CallTo(() => user.Identity)
                .Returns(identity);

            A.CallTo(() => userManager.GetLoginsAsync(identity))
                .Returns(logins);

            var result = await sut.GetLoginsAsync(user, ct);

            Assert.Same(logins, result);
        }

        [Fact]
        public async Task Create_should_add_user()
        {
            var identity = CreateIdentity(found: false);

            var values = new UserValues
            {
                Email = identity.Email
            };

            SetupCreation(identity, 1);

            await sut.CreateAsync(values.Email, values, ct: ct);

            A.CallTo(() => userEvents.OnUserRegisteredAsync(A<IUser>.That.Matches(x => x.Identity == identity)))
                .MustHaveHappened();

            A.CallTo(() => userEvents.OnConsentGivenAsync(A<IUser>.That.Matches(x => x.Identity == identity)))
                .MustNotHaveHappened();

            A.CallTo(() => userManager.AddToRoleAsync(identity, A<string>._))
                .MustNotHaveHappened();

            A.CallTo(() => userManager.AddPasswordAsync(identity, A<string>._))
                .MustNotHaveHappened();

            A.CallTo(() => userManager.SetLockoutEndDateAsync(identity, A<DateTimeOffset>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task Create_should_raise_event_if_consent_given()
        {
            var identity = CreateIdentity(found: false);

            var values = new UserValues
            {
                Consent = true
            };

            SetupCreation(identity, 1);

            await sut.CreateAsync(identity.Email, values, ct: ct);

            A.CallTo(() => userEvents.OnConsentGivenAsync(A<IUser>.That.Matches(x => x.Identity == identity)))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Create_should_set_admin_if_first_user()
        {
            var identity = CreateIdentity(found: false);

            var values = new UserValues
            {
                Consent = true
            };

            SetupCreation(identity, 0);

            await sut.CreateAsync(identity.Email, values, ct: ct);

            A.CallTo(() => userManager.AddToRoleAsync(identity, NotifoRoles.HostAdmin))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Create_should_not_lock_first_user()
        {
            var identity = CreateIdentity(found: false);

            var values = new UserValues
            {
                Consent = true
            };

            SetupCreation(identity, 0);

            await sut.CreateAsync(identity.Email, values, true, ct);

            A.CallTo(() => userManager.SetLockoutEndDateAsync(identity, A<DateTimeOffset>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task Create_should_lock_second_user()
        {
            var identity = CreateIdentity(found: false);

            var values = new UserValues
            {
                Consent = true
            };

            SetupCreation(identity, 1);

            await sut.CreateAsync(identity.Email, values, true, ct);

            A.CallTo(() => userManager.SetLockoutEndDateAsync(identity, InFuture()))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Create_should_add_password()
        {
            var identity = CreateIdentity(found: false);

            var values = new UserValues
            {
                Password = "password"
            };

            SetupCreation(identity, 1);

            await sut.CreateAsync(identity.Email, values, ct: ct);

            A.CallTo(() => userManager.AddPasswordAsync(identity, values.Password))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_throw_exception_if_not_found()
        {
            var update = new UserValues
            {
                Email = "new@email.com"
            };

            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.UpdateAsync(identity.Id, update, ct: ct));
        }

        [Fact]
        public async Task Update_should_do_nothing_for_new_update()
        {
            var update = new UserValues();

            var identity = CreateIdentity(found: true);

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo(() => userEvents.OnUserUpdatedAsync(A<IUser>.That.Matches(x => x.Identity == identity), A<IUser>._))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_add_to_role()
        {
            var update = new UserValues
            {
                Roles = new HashSet<string>
                {
                    "admin"
                }
            };

            var identity = CreateIdentity(found: true);

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo(() => userManager.AddToRoleAsync(identity, "admin"))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_remove_from_role()
        {
            var update = new UserValues
            {
                Roles = new HashSet<string>()
            };

            var identity = CreateIdentity(found: true);

            A.CallTo(() => userManager.GetRolesAsync(identity))
                .Returns(new List<string> { "admin" });

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo(() => userManager.RemoveFromRoleAsync(identity, "admin"))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_change_password_if_changed()
        {
            var update = new UserValues
            {
                Password = "password"
            };

            var identity = CreateIdentity(found: true);

            A.CallTo(() => userManager.HasPasswordAsync(identity))
                .Returns(true);

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo(() => userManager.RemovePasswordAsync(identity))
                .MustHaveHappened();

            A.CallTo(() => userManager.AddPasswordAsync(identity, update.Password))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_change_email_if_changed()
        {
            var update = new UserValues
            {
                Email = "new@email.com"
            };

            var identity = CreateIdentity(found: true);

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo(() => userManager.SetEmailAsync(identity, update.Email))
                .MustHaveHappened();

            A.CallTo(() => userManager.SetUserNameAsync(identity, update.Email))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_set_claim_if_consent_given()
        {
            var update = new UserValues
            {
                Consent = true
            };

            var identity = CreateIdentity(found: true);

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo<Task<IdentityResult>>(() => userManager.AddClaimsAsync(identity, HasClaim(NotifoClaimTypes.Consent)))
                .MustHaveHappened();

            A.CallTo(() => userEvents.OnConsentGivenAsync(A<IUser>.That.Matches(x => x.Identity == identity)))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Update_should_set_claim_if_email_consent_given()
        {
            var update = new UserValues
            {
                ConsentForEmails = true
            };

            var identity = CreateIdentity(found: true);

            await sut.UpdateAsync(identity.Id, update, ct: ct);

            A.CallTo<Task<IdentityResult>>(() => userManager.AddClaimsAsync(identity, HasClaim(NotifoClaimTypes.ConsentForEmails)))
                .MustHaveHappened();

            A.CallTo(() => userEvents.OnConsentGivenAsync(A<IUser>.That.Matches(x => x.Identity == identity)))
                .MustHaveHappened();
        }

        [Fact]
        public async Task SetPassword_should_throw_exception_if_not_found()
        {
            var password = "password";

            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.SetPasswordAsync(identity.Id, password, null, ct));
        }

        [Fact]
        public async Task SetPassword_should_succeed_if_found()
        {
            var password = "password";

            var identity = CreateIdentity(found: true);

            await sut.SetPasswordAsync(identity.Id, password, null, ct);

            A.CallTo(() => userManager.AddPasswordAsync(identity, password))
                .MustHaveHappened();
        }

        [Fact]
        public async Task SetPassword_should_change_password_if_identity_has_password()
        {
            var password = "password";

            var identity = CreateIdentity(found: true);

            A.CallTo(() => userManager.HasPasswordAsync(identity))
                .Returns(true);

            await sut.SetPasswordAsync(identity.Id, password, "old", ct);

            A.CallTo(() => userManager.ChangePasswordAsync(identity, "old", password))
                .MustHaveHappened();
        }

        [Fact]
        public async Task AddLogin_should_throw_exception_if_not_found()
        {
            var login = A.Fake<ExternalLoginInfo>();

            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.AddLoginAsync(identity.Id, login, ct));
        }

        [Fact]
        public async Task AddLogin_should_succeed_if_found()
        {
            var login = A.Fake<ExternalLoginInfo>();

            var identity = CreateIdentity(found: true);

            await sut.AddLoginAsync(identity.Id, login, ct);

            A.CallTo(() => userManager.AddLoginAsync(identity, login))
                .MustHaveHappened();
        }

        [Fact]
        public async Task RemoveLogin_should_throw_exception_if_not_found()
        {
            var provider = "my-provider";
            var providerKey = "key";

            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.RemoveLoginAsync(identity.Id, provider, providerKey, ct));
        }

        [Fact]
        public async Task RemoveLogin_should_succeed_if_found()
        {
            var provider = "my-provider";
            var providerKey = "key";

            var identity = CreateIdentity(found: true);

            await sut.RemoveLoginAsync(identity.Id, provider, providerKey, ct);

            A.CallTo(() => userManager.RemoveLoginAsync(identity, provider, providerKey))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Lock_should_throw_exception_if_not_found()
        {
            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.LockAsync(identity.Id, ct));
        }

        [Fact]
        public async Task Lock_should_succeed_if_found()
        {
            var identity = CreateIdentity(found: true);

            await sut.LockAsync(identity.Id, ct);

            A.CallTo<Task<IdentityResult>>(() => userManager.SetLockoutEndDateAsync(identity, InFuture()))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Unlock_should_throw_exception_if_not_found()
        {
            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.UnlockAsync(identity.Id, ct));
        }

        [Fact]
        public async Task Unlock_should_succeeed_if_found()
        {
            var identity = CreateIdentity(found: true);

            await sut.UnlockAsync(identity.Id, ct);

            A.CallTo(() => userManager.SetLockoutEndDateAsync(identity, null))
                .MustHaveHappened();
        }

        [Fact]
        public async Task Delete_should_throw_exception_if_not_found()
        {
            var identity = CreateIdentity(found: false);

            await Assert.ThrowsAsync<DomainObjectNotFoundException>(() => sut.DeleteAsync(identity.Id, ct));

            A.CallTo(() => userEvents.OnUserDeletedAsync(A<IUser>._))
                .MustNotHaveHappened();
        }

        [Fact]
        public async Task Delete_should_succeed_if_found()
        {
            var identity = CreateIdentity(found: true);

            await sut.DeleteAsync(identity.Id, ct);

            A.CallTo(() => userManager.DeleteAsync(identity))
                .MustHaveHappened();

            A.CallTo(() => userEvents.OnUserDeletedAsync(A<IUser>.That.Matches(x => x.Identity == identity)))
                .MustHaveHappened();
        }

        private IdentityUser CreateIdentity(bool found, string id = "123")
        {
            var identity = CreatePendingUser(id);

            if (found)
            {
                A.CallTo(() => userManager.FindByIdAsync(identity.Id))
                    .Returns(identity);

                A.CallTo(() => userManager.FindByEmailAsync(identity.Email))
                    .Returns(identity);
            }
            else
            {
                A.CallTo(() => userManager.FindByIdAsync(identity.Id))
                    .Returns(Task.FromResult<IdentityUser>(null!));

                A.CallTo(() => userManager.FindByEmailAsync(identity.Email))
                    .Returns(Task.FromResult<IdentityUser>(null!));
            }

            return identity;
        }

        private void SetupCreation(IdentityUser identity, int numCurrentUsers)
        {
            var users = new List<IdentityUser>();

            for (var i = 0; i < numCurrentUsers; i++)
            {
                users.Add(CreatePendingUser(i.ToString(CultureInfo.InvariantCulture)));
            }

            A.CallTo(() => userManager.Users)
                .Returns(users.AsQueryable());

            A.CallTo(() => userFactory.Create(identity.Email))
                .Returns(identity);
        }

        private static IEnumerable<Claim> HasClaim(string claim)
        {
            return A<IEnumerable<Claim>>.That.Matches(x => x.Any(y => y.Type == claim));
        }

        private static DateTimeOffset InFuture()
        {
            return A<DateTimeOffset>.That.Matches(x => x >= DateTimeOffset.UtcNow.AddYears(1));
        }

        private static IdentityUser CreatePendingUser(string id = "123")
        {
            return new IdentityUser
            {
                Id = id,
                Email = $"{id}@email.com"
            };
        }
    }
}
