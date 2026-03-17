// =============================================================================
// ExampleLifetimeScope.cs
// VContainer の LifetimeScope 設定例
// =============================================================================

using Cortis;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Example
{
    public sealed class ExampleLifetimeScope : LifetimeScope
    {
        [SerializeField] Transform target;

        protected override void Configure(IContainerBuilder builder)
        {
            // Gateway の登録
            builder.Register<FlutterMessageGateway>(Lifetime.Singleton)
                .As<IMessageGateway>();

            // Command + Event Presenter の登録
            ExamplePresenter.Register(builder, Lifetime.Singleton);

            // Event-only Presenter の登録
            EventOnlyPresenter.Register(builder, Lifetime.Singleton);

            // Routed Presenter の登録（inner 型で登録するが、Gateway は root 型で通信する）
            RoutedPresenter.Register(builder, Lifetime.Singleton);
        }
    }
}
