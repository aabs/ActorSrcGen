# Pull Request Guidelines

Thank you for considering contributing to our project!  We appreciate your time
and effort to improve it.

## Getting Started

Before you begin, please make sure you have the following:

1. *Fork the repository*: Fork the project repository on GitHub to your own
   account.
1. *Clone the repository*: Clone the forked repository to your local machine
   using Git.
1. *Create a new branch*: Create a new branch on your local machine for your
   changes.  Use a descriptive name for your branch.

## Contribution Guidelines
Before submitting a pull request, please ensure that your changes adhere to the
following guidelines:

1. *Code style*: Ensure that your code follows the coding style and conventions
   of the project.  This includes indentation, naming conventions, and best
   practices.
1. *Testing*: Make sure to add or update tests to cover the changes you made.
   This helps ensure the stability and reliability of the project.
1. *TDD first*: Add a failing test before implementing a fix/feature. See the
   workflow in [specs/001-generator-reliability-hardening/quickstart.md](specs/001-generator-reliability-hardening/quickstart.md).
1. *Coverage*: Keep overall coverage at or above 85% and maintain 100% on
   critical paths (Generator, ActorVisitor, ActorGenerator). Run `dotnet test
   /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura` before opening a
   pull request.
1. *Docs*: Update relevant docs when changing diagnostics, behaviors, or
   public-facing APIs. See [doc/DIAGNOSTICS.md](doc/DIAGNOSTICS.md) for
   diagnostic messaging guidance.