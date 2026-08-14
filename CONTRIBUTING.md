
# HowTo Contribute

This is a guide to start contributing to the Chargy Transparency Software project:

- Make yourself comfortable with Chargy by e.g. installing it or reading the source code.
- Read the documentation to get an idea of how the software works (architecture documents), how the community works (this document).
- Find an open issue to work on or fire an new issue.
- Assign yourself to the issue and start contributing.
- Start contributing by using the procedures mentioned in this chapter.

## Contributor License Agreement

We ask each of our contributors to sign our contributor license agreement (CLA).
This has advantages for both parties, it gives you the assurance that your
contribution will remain available under the AGPL 3.0 license. Meanwhile, you
give your code in license to us, so we can add your code to the Chargy Transparency
Software. And we know your contribution is entirely your work, so we don't get
in trouble with legal issues. Please read the CLA text carefully.

## Submitting code
To submit changes to the Chargy Transparency Software branches:

- Create a fork of the Chargy Transparency Software repository you will be working in
- Make and commit your changes to your fork
- Create a pull request to merge the changes into the right branch. If the
  changes fix a bug, mention the issue number in the commit message or pull
  request message (example: fixes #101, solved #87). If no ticket exists, create
  one beforehand. Afterwards, please update the relevant documentation.

## Coding style

ChargyCore.NET follows the coding style of the other GraphDefined .NET projects
([Hermod](https://github.com/Vanaheimr/Hermod), [Styx](https://github.com/Vanaheimr/Styx)):

- Block-scoped namespaces, `#region` sections
- BCL type names: `Byte`, `String`, `Boolean`, `UInt32`, ...
- Aligned declarations and assignments
- Nullable reference types enabled
- The AGPL license header at the top of every source file

## Verification report parity with ChargyCore.TS

The `*.expected.txt` files under `ChargyCoreTests/TestData/` are shared, byte-for-byte,
with [ChargyCore.TS](https://github.com/OpenChargingCloud/ChargyCore.TS). They are the
contract that keeps both implementations semantically identical.

Never "fix" an expected file to make a test pass. If the .NET output differs, either the
port has a bug, or the behaviour genuinely changed — in which case the change belongs in
both repositories, in the same pull request cycle.
