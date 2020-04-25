# Extracting the list of dependencies

Following command (run under `bash`) will extract the list of dependencies:
`mvn dependency:list | sed -ne s/..........// -e /patterntoexclude/d -e s/:compile//p -e s/:runtime//p | sort | uniq`

`amazon-kinesis-client-multilang` will contain the dependency on the latest `amazon-kinesis-client` that might not be in the repo yet - downgrade the reference to latest from public maven repo

# Patching JAR list in `Bootstrap.cs`

Regex to match the results of the command from above:
`(?<grp>[a-zA-Z0-9\-\._]+):(?<name>[a-zA-Z0-9\-\._]+):jar:(?<ver>[a-zA-Z0-9\-\._]+)`

Replacement command (`Expresso` syntax):
`new MavenPackage("${grp}", "${name}", "${ver}"),`