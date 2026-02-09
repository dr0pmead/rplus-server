using System;

namespace RPlus.Wallet.Domain.Exceptions;

public sealed class ConcurrencyException(string message) : Exception(message);
