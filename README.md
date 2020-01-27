# ProcessIsolation

The ProcessIsolation project tries to provide a means of running (managed) code isolated from one another
using separate processes. This is one of the recommended scenarios that Microsoft suggests, now that
AppDomains are no more.

The basic architecture is conceptually rather simple. You have your own application (called the host),
that wants to run code inside isolated containers, so it doesn't interfere with stability of your
host process. This project uses operating system processes as those containers.

The host references the `ProcessIsolation.dll` assembly, which contains all the code required to
manage the isolation processes. The isolation processes themselves are instances of the `pihost.exe`
executable, that uses the .NET `AssemblyLoadContext`-class to load the code that should actually
run in isolation.

By using `AssemblyLoadContext`, the isolated code may even reference different versions of dependencies
than the `pihost.exe` and thus provides maximum flexibility in this regard.

Additionally, the host can specify a number of properties for the isolation process. Including
resource limits (like CPU or memory usage). Also, the ProcessIsolation framework ensures that
no orphaned processes remain should the host crash. It also allows the host to transparently
restart an isolation process, should that be required.

The ProcessIsolation framework comes as three nuget packages:

* `ProcessIsolation`: contains the host side part of the framework
* `ProcessIsolation.Host`: contains the `pihost.exe` executable
* `ProcessIsolation.Shared`: contains shared assemblies used by both of the above

An example for a host process is available as [..\example\SampleHost](..\example\SampleHost).
Typical code that could be loaded into an isolation process is available as [..\example\SampleLib](..\example\SampleLib).
Note that the this code has no reference to `ProcessIsolation`.

Don't get bogged down by the apparent complexity of the SampleHost. It also serves as a testbed
to exercise most functions that the ProcessIsolation framework provides and thus has a multitude
of command line options in this regard.

The basic code to run code (from another assembly) in isolation is this:

```C#
var options = new IsolationOptions();
using (var isolator = await ProcessIsolator.CreateAsync(options).ConfigureAwait(false))
{
    int res = await isolator.InvokeMethodAsync("assembly.dll", "namespace.class.method").ConfigureAwait(false);
    await isolator.WaitForExitAsync().ConfigureAwait(false);
}
```

As you can see the current interface is somewhat limited. Basically it mimics the `AppDomain.ExecuteAssembly()`-method.

To make it work, the assembly being specified with the `InvokeMethodAsync()`-method must have a method with the following
signature:

```C#
    public static int Method(string[] args)
```

That is, it must
* be public
* be static
* have a return type of `int` (returning `0` means OK, anything else an error)
* accept a `string[]` as arguments

Thus, the interface between the host process and the isolation process (and thus the code running within) is rather
arcane. This is so, because in this first attempt of the project we didn't want to settle on a n IPC mechanism yet.

As actual value of plain `System.Diagnostics.Process` this project - as said above - provides a number of options,
settings and APIs to manage and handle the isolated processes. Explore the `IsolationOptions` and `ProcessIsolator`
classes for more information.

The later is of course required in some form to allow complex communication between the host and the code in the
isolation process. Something like remoting (which is no longer available).

Currently, gRPC is not an option as it requires a full HTTP/2 capable stack and thus would make Kestrel a dependency.
Also gRPC at the moment doesn't allow for local-host-only IPC or something based on named pipes. All in all that 
seems a bit heavyweight at the moment.

Other options exist of course, as this project is using [`jacqueskang/IpcServiceFramework`](https://github.com/jacqueskang/IpcServiceFramework) internally at the moment.

Finally, there is nothing preventing a host and isolated user code to establish their on IPC (even gRPC) on top.

To build the project use the `build.ps` file:

```PS
PS> build.ps1 -Tidy -Build -Test -Example
```
