using System.Collections.Generic;
using UnityEngine;

public interface IConfigValidator<in T>
{
    List<ValidationIssue> Validate(T config);
}