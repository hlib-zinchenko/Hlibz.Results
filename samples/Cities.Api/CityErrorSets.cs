namespace Cities.Api;

// Error sets: the errors each operation declares. Callers switch over them exhaustively, and the
// endpoints document each one.
public union GetCityError(CityNotFound);

public union CreateCityError(CityNameTaken);

public union DeleteCityError(CityNotFound, CityInUse);
