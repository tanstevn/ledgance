using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Shared.Unit.Tests.Behaviors {
    public class ProtectedRequest : ICommand<Result<string>> {
        public string Name { get; set; } = string.Empty;
    }

    [AllowAnonymousRequest]
    public class PublicRequest : ICommand<Result<string>> { }

    [RequiresPermission(SharedPermissions.MembersManage)]
    public class MemberManagementRequest : ICommand<Result<string>> { }

    [RequiresEntitlement(ProductModule.Audit, Entitlements.AdvancedReview)]
    public class AdvancedReviewRequest : ICommand<Result<string>> { }

    public class EchoHandler<TRequest> : IRequestHandler<TRequest, Result<string>>
        where TRequest : IRequest<Result<string>> {
        public bool WasCalled { get; private set; }

        public Task<Result<string>> HandleAsync(TRequest request, CancellationToken ct) {
            WasCalled = true;
            return Task.FromResult(Result<string>.Success("handled"));
        }
    }

    public class ProtectedRequestValidator : AbstractValidator<ProtectedRequest> {
        public ProtectedRequestValidator() {
            RuleFor(request => request.Name).NotEmpty().WithMessage("Name is required.");
        }
    }

    public class AuthorizationBehaviorTests {
        [Fact]
        public async Task An_unauthenticated_caller_is_rejected_before_the_handler_runs() {
            var handler = new EchoHandler<ProtectedRequest>();

            var harness = new MediatorTestHarness()
                .WithHandler<ProtectedRequest, Result<string>>(handler);

            await Assert.ThrowsAsync<UnauthenticatedException>(
                () => harness.SendAsync(new ProtectedRequest { Name = "valid" }));

            Assert.False(handler.WasCalled);
        }

        [Fact]
        public async Task An_authenticated_caller_reaches_the_handler() {
            var harness = new MediatorTestHarness(TestIdentity.User())
                .WithHandler<ProtectedRequest, Result<string>>(new EchoHandler<ProtectedRequest>());

            var result = await harness.SendAsync(new ProtectedRequest { Name = "valid" });

            Assert.True(result.Successful);
        }

        [Fact]
        public async Task An_anonymous_request_runs_without_a_caller() {
            var harness = new MediatorTestHarness()
                .WithHandler<PublicRequest, Result<string>>(new EchoHandler<PublicRequest>());

            var result = await harness.SendAsync(new PublicRequest());

            Assert.True(result.Successful);
        }

        [Fact]
        public async Task A_caller_without_the_required_permission_is_forbidden() {
            var handler = new EchoHandler<MemberManagementRequest>();

            var harness = new MediatorTestHarness(
                    TestIdentity.UserWithRegisteredPermissions(OrganizationRole.Member))
                .WithHandler<MemberManagementRequest, Result<string>>(handler);

            var exception = await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(new MemberManagementRequest()));

            Assert.Contains(SharedPermissions.MembersManage, exception.Message);
            Assert.False(handler.WasCalled);
        }

        [Fact]
        public async Task A_caller_holding_the_permission_is_allowed_through() {
            var harness = new MediatorTestHarness(
                    TestIdentity.UserWithRegisteredPermissions(OrganizationRole.Admin))
                .WithHandler<MemberManagementRequest, Result<string>>(
                    new EchoHandler<MemberManagementRequest>());

            var result = await harness.SendAsync(new MemberManagementRequest());

            Assert.True(result.Successful);
        }
    }

    public class EntitlementBehaviorTests {
        [Fact]
        public async Task A_capability_missing_from_the_plan_stops_the_request() {
            var handler = new EchoHandler<AdvancedReviewRequest>();

            var harness = new MediatorTestHarness(TestIdentity.User())
                .WithHandler<AdvancedReviewRequest, Result<string>>(handler);

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditMicro);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(new AdvancedReviewRequest()));

            Assert.Contains(Entitlements.AdvancedReview, exception.Message);
            Assert.False(handler.WasCalled);
        }

        [Fact]
        public async Task A_plan_including_the_capability_allows_the_request() {
            var harness = new MediatorTestHarness(TestIdentity.User())
                .WithHandler<AdvancedReviewRequest, Result<string>>(
                    new EchoHandler<AdvancedReviewRequest>());

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditSmall);

            var result = await harness.SendAsync(new AdvancedReviewRequest());

            Assert.True(result.Successful);
        }
    }

    public class ValidationBehaviorTests {
        [Fact]
        public async Task An_invalid_request_never_reaches_the_handler() {
            var handler = new EchoHandler<ProtectedRequest>();

            var harness = new MediatorTestHarness(TestIdentity.User())
                .WithHandler<ProtectedRequest, Result<string>>(handler)
                .WithValidator<ProtectedRequest>(new ProtectedRequestValidator());

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => harness.SendAsync(new ProtectedRequest()));

            Assert.Contains(exception.Errors, error => error.ErrorMessage == "Name is required.");
            Assert.False(handler.WasCalled);
        }

        [Fact]
        public async Task A_request_with_no_validator_registered_passes_through() {
            var harness = new MediatorTestHarness(TestIdentity.User())
                .WithHandler<ProtectedRequest, Result<string>>(new EchoHandler<ProtectedRequest>());

            var result = await harness.SendAsync(new ProtectedRequest());

            Assert.True(result.Successful);
        }

        [Fact]
        public async Task Authorization_runs_before_validation_so_invalid_input_is_not_echoed_to_strangers() {
            var harness = new MediatorTestHarness()
                .WithHandler<ProtectedRequest, Result<string>>(new EchoHandler<ProtectedRequest>())
                .WithValidator<ProtectedRequest>(new ProtectedRequestValidator());

            await Assert.ThrowsAsync<UnauthenticatedException>(
                () => harness.SendAsync(new ProtectedRequest()));
        }
    }
}
