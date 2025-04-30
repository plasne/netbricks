# Configuration Management

## Why?

I have a number of tenants I believe all configuration management solutions should have. I have tried to implement them in a way that is easy to use and understand.

- Configuration should be validated and conformed on startup. It is beneficial to terminate the execution of our service during startup when it has an invalid configuration, before it starts processing requests. When validating, the configuration should also be conformed to the expected datatypes and ranges. When the configuration value do not conform, it should fail validation or use a default.

- Configuration should be logged on startup. When troubleshooting issues with the service, it can be invaluable to look at the startup logs to see how the service was configured. While we do not want to show secrets, it is still important to show whether the value is set or not.

- Configuration should support secrets in a safe way. This solution should enable developers to store secrets easily and safely. It should make it easy to do the right thing.

- Configuration should be easy for administrators to set properly. There are several considerations here:

  - Configuration values should have reasonable defaults whenever possible.
  - Very specific configuration values may derive from more generic configuration values. For instance, if there are 4 places in the code that need a retry interval, consider using 4 configuration values that all default to using the value of a single configuration value.
  - Consider allowing for "templates" or "modes" that can be set as a single configuration value that sets many other configuration values.
  - Configuration values can interact with one another. For instance, if one value is true, other values may be required. These more complex validations should be enforced.
  - Configuration should be documented extensively and clearly.

## Comparison

I have evaluated a number of configuration management solutions and have found that most of them do not meet all of these requirements or don't cover the same scope as this solution.
