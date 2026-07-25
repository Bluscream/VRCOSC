// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.App.Nodes.Types;
using VRCOSC.App.SDK.Modules;

namespace VRCOSC.App.SDK.Nodes;

public interface IModuleNode<T> where T : Module
{
    public T Module { get; set; }
}

public abstract class ModuleNode<T> : Node, IModuleNode<T> where T : Module
{
    public virtual T Module { get; set; } = null!;
}