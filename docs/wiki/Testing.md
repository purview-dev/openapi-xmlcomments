# Testing

Four layers, each covering what the one before cannot.

| Layer | Where | Proves |
| --- | --- | --- |
| Unit | `src/tests/XmlComments.UnitTests` | The rules (ID normalisation, which IDs are removed, overlap detection) and both tasks, run in-process against a mocked `IBuildEngine`. |
| Integration | `src/tests/XmlComments.IntegrationTests` | Real consumer solutions, built with the real OpenAPI generator on `net10.0` and `net11.0`, importing the props and targets from source. Each API requests its own `/openapi/v1.json`. The same consumers are diagnosed with the skill's script, so its report stays accurate. |
| Repository | `src/tests/Repository.UnitTests` | Every authored skill conforms to the Agent Skills specification and carries the product prefix. |
| Packed consumer | `scripts/test-packed-consumer.ps1` | The `.nupkg` itself, through `PackageReference` alone: the `buildTransitive` import, the packed task path, and nothing left in the build output. |

The integration suite includes `Build_GivenDeduplicationDisabled_ReproducesTheDuplicateKeyFailure`, which turns the
package off and asserts the generator still fails. It is what shows the other tests would notice a regression.

```text
just test               # every tier
just test-unit          # unit + repository
just test-integration
just smoke-packed-consumer
```

The integration tier needs `pwsh` on the path for the script tests; GitHub's hosted runners have it.

Every consumer compiles the same documented public attribute into the API and each library, which is what a source
generator's post-initialization output does. The tests therefore depend on no particular generator.
