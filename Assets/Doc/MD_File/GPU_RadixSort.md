# GPU Radix Sort
### GPU radix sort V1 
- (1 bit version) in one pass
    1. get input
    2. get "bit" as b
    3. "inverse 'bit'" as E (if it is 0, count as 1)(bucket of 0!!!)
    4. Scan the 1s of E as F
    5. TotalFasle = e[last] + f[last] (total number of 0)
    6. T = i - F +  TotalFasle
    7. (address) d = b ? T:F
    8. Scatter by d

- Scan process
    1. UP Scan 
    2. DOWN Scan

### GPU radix sort V2
- (n bit version) in one pass (n = 4,8...)
- total for 32/n passs if is uint
    1.      get input single bit
    2.      For i = 0 to 2^n -1
    3.          For j = 0 to n-1
    4.              bit[j] == i ? b[i][j] = 1 : b[i][j] = 0;
    5.          For j = 0 to n-1
    6.              F[i][j] = b[i][j].EScan();
    7.      For i = 0 to 2^n -1
    8.          For j = 0 to n-1 : if b[i][j] == 1 
    9.           Offset[j] = F[i][j] + (i-1<0 ? 0 : F[i-1][n-1].IScan());
    10.     bit.Scatter(Offset[]);

### 1-bit Example 
input : 1  3  2  6  5  4  7  4
--------------------------------
// bucket == 0
b[0] =  0  0  1  1  0  1  0  1
F[0] =  0  0  0  1  2  2  3  3

// bucket == 1
b[1] =  1  1  0  0  1  0  1  0
F[1] =  0  1  2  2  2  3  3  4

// offset 
        4  5  0  1  6  2  7  3

// result
        2  6  4  4  1  3  5  7

### Counting sort Example (GPU)
input : 1  3  2  6  5  4  7  4

// Count
index:  0  1  2  3  4  5  6  7
count:  0  1  1  1  2  1  1  1

// Scan: starting position
count:  0  0  1  2  3  5  6  7      // IScan = 8

// Scatter by key: address = count[key]++;
Key:    Unknow but needed 

address:0  2  1  6  5  3  7  3+1    
address:0  2  1  6  5  3  7  4   
input : 1  3  2  6  5  4  7  4
    // InterlockedAdd(Counts[4]) = 3+1
    // Counts[4] = 3    // after scan

// Result: 
count:  1  2  3  4  4  5  6  7