namespace BindingsGeneration;

public interface IFactory<T, U> {
    bool Handles(T thing);
    U Construct ();
}