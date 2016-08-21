# DependencyInjectionToolset

Dependency injection is awesome. It helps you build code that is loosely-coupled, testable, maintainable. The code is
clean and readable. And if set up properly, you might end up coding a lot less than without dependency injection.  

Almost :) Because if you use constructor injection (and why wouldn't you if you apply the basic OO principles), you might end
up creating a lot of constructors with a lot of parameters. While this is not a bad thing - after all this gives you an instant
overview about the dependencies of a component - you do have to code a lot.

This tool helps you with that. Features currently include:
* Get the cursor on a private readonly field of an interface or abstract class type. Hit Ctrl+. (or whatever is your
shortcut for the refactoring suggestions) and choose "Generate dependency injection constructor". This will give you a 
constructor which has a parameter for every private readonly field of an interface or abstract class type and the fields
are initialized from the parameters.

* Get your cursor over a constructor parameter. Now hit Ctrl+. and you get two options: you can generate a private
readonly field that is of the same type as your constructor parameter and you have the option to name the field the
same as your parameter, or prefix the name of the parameter with "_".

# Install

Please check the license agreement for terms and conditions.  

You can download the extension from the Visual Studio Gallery:  
https://visualstudiogallery.msdn.microsoft.com/319cb092-4d7e-429a-894d-ac33e1e78c1b


# Credits

Thanks for [@trydis](https://github.com/trydis) for publishing his version of the "Introduce and initalize field" feature.
Check out his blog post at http://trydis.github.io/2015/01/03/roslyn-code-refactoring/

Also thanks for [@varsi94](https://github.com/varsi94) for helping out with the original version of the code.

<div>Icons made by <a href="http://www.freepik.com" title="Freepik">Freepik</a> from <a href="http://www.flaticon.com" title="Flaticon">www.flaticon.com</a> is licensed by <a href="http://creativecommons.org/licenses/by/3.0/" title="Creative Commons BY 3.0" target="_blank">CC 3.0 BY</a></div>

# Pull requests and ideas are always welcome. :) 