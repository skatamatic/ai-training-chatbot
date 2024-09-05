# The Sorcerer  
*A powerful C# unit test generator*

## Overview

The Sorcerer automates the creation, enhancement, and validation of unit tests in C# through a well-structured workflow. It's designed to streamline the testing process, making it easier to maintain high-quality, well-documented tests with minimal manual effort. The console app is located in the `Sorcerer.Console` directory.

---

## Operation

The Sorcerer follows a lifecycle for test generation, from initialization to verification. Here’s an overview:

### 1. Initialization
The Sorcerer is configured with necessary settings, such as the target files for testing and the types of enhancements to apply.

### 2. Test Preparation
The Sorcerer performs a codebase-wide analysis, gathering all relevant context. If suitable tests already exist, and the configuration allows, it skips test generation and moves to enhancement.

### 3. Test Generation
If no tests are found, the Sorcerer generates baseline unit tests to ensure coverage.

### 4. Enhancement
Enhancements are applied to improve test quality, which may include refactoring, increasing test coverage, or adding documentation. These enhancements are configurable.

### 5. Verification
The Sorcerer runs the generated tests to verify their correctness. Failed tests trigger automated fixing attempts, repeating the process until success or maximum retries are reached.

### 6. Feedback
The CLI provides extensive feedback on the Sorcerer's progress.

---

## Configuration

The Sorcerer is highly configurable through a `config` file. API keys must be stored in a `secrets.json` file in the same directory. Here's a breakdown of the configuration settings:

### OpenAIConfig
| Parameter | Description |
|-----------|-------------|
| ApiKey    | The API key for authenticating OpenAI requests. |
| Model     | Specifies the AI model (default: GPT-3.5-Turbo, can be set to GPT-4 models). |
| Mode      | Defines the operational mode, such as "completions". |
| MaxTokens | Limits the number of tokens per API request. |
| EnableFunctions | Enables/disables function usage in responses. |
| TransmitFunctionResults | Includes function results in session history if enabled. |
| SystemPrompt | Adds system-level context to guide AI responses. |

### GenerationConfig
| Parameter | Description |
|-----------|-------------|
| IssueContextLineCount | Number of lines of code context for issues/tests. |
| ContextSearchDepth | How deeply to search through code for context. |
| StylePrompt | Guidelines for writing structured and stylistic unit tests. |

### SorcererConfig
| Parameter | Description |
|-----------|-------------|
| MaxFixAttempts | Maximum attempts to automatically fix failing tests. |
| SkipToEnhanceIfTestsExist | Skips test generation if existing tests are found. |
| FileToTest | File that undergoes unit testing or enhancement. |
| Mode | Operational mode (Unity or .NET). |
| Beautify | Beautifies output with syntax highlighting/markdown. |
| PresentationMode | Experimental mode for paused output during presentations. |

---

## Enhancement Types

The Sorcerer applies various enhancements to improve unit test quality:

| Enhancement Type | Description |
|------------------|-------------|
| General | Enhances overall test quality, coverage, and fixes bugs. |
| Coverage | Adds edge case tests, `[TestCases]`, `[Values]`, etc. |
| Refactor | Refactors code to improve clarity and best practices. |
| Document | Adds meaningful comments and improves variable/method names. |
| SquashBugs | Fixes bugs in tests, bad logic, and incorrect assertions. |
| Clean | Cleans up redundant test cases and enforces consistent style. |
| Verify | Ensures correctness of tests after enhancement. |
| Assess | Provides detailed test quality assessment without changes. |

---

## DI Architecture

The Sorcerer uses Dependency Injection (DI) for flexibility, following the .NET Core DI framework. This allows for a modular structure where services can be injected and managed by the DI container.

### 1. Service Registration
Services like `IUnitTestRunner`, `IUnitTestGenerator`, and `ISolutionTools` are registered based on the configuration and mode (Unity or .NET).

### 2. Service Scope and Lifecycle
Key services such as `IUnitTestSorcerer`, `IUnitTestFixer`, and `IUnitTestEnhancer` are registered as singletons, ensuring that they persist for the application's lifetime.

### 3. Flexible Configuration
Mode-based services allow switching between Unity and .NET modes without changing the core codebase.

---

## Unity Integration

The Sorcerer supports integration with Unity projects for automated unit test generation, enhancement, and verification.

### Unity Web Client Test Runner
The `UnityWebClientTestRunner` interacts with Unity Editor through a custom web server script (`SorcererWebServer`), enabling automated test runs.

- **Batch Mode Execution**: Run tests in Unity’s batch mode for CI/CD pipelines.
- **Test Filtering**: Use regex patterns to filter and execute specific tests.

### Workflow

1. **Setup**: Ensures the server script is running and ready to execute commands.
2. **Test Execution**: Sends commands to Unity Editor to compile and run tests.
3. **Recompilation**: Can recompile scripts and run tests in play or edit mode.
