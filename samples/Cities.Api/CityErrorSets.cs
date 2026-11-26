namespace Cities.Api;

// Error sets: the errors each operation declares. Callers switch over them exhaustively, and the
// endpoints document each one. A union compiles to a struct without equality, which CA1815 flags.
#pragma warning disable CA1815
public union GetCityError(CityNotFound);

public union CreateCityError(CityNameTaken);

public union DeleteCityError(CityNotFound, CityInUse);
#pragma warning restore CA1815
